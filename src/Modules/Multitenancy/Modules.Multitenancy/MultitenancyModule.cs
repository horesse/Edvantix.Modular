using Asp.Versioning;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Eventing.Abstractions;
using EDV.Framework.Persistence;
using EDV.Framework.Shared.Identity;
using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Web.Modules;
using EDV.Modules.Multitenancy.Contracts;
using EDV.Modules.Multitenancy.Contracts.Authorization;
using EDV.Modules.Multitenancy.Data;
using EDV.Modules.Multitenancy.Features.v1.AdjustTenantValidity;
using EDV.Modules.Multitenancy.Features.v1.ChangeTenantActivation;
using EDV.Modules.Multitenancy.Features.v1.CreateTenant;
using EDV.Modules.Multitenancy.Features.v1.GetMyTenantStatus;
using EDV.Modules.Multitenancy.Features.v1.GetTenantMigrations;
using EDV.Modules.Multitenancy.Features.v1.GetTenants;
using EDV.Modules.Multitenancy.Features.v1.GetTenantStatus;
using EDV.Modules.Multitenancy.Features.v1.GetTenantTheme;
using EDV.Modules.Multitenancy.Features.v1.RenewTenant;
using EDV.Modules.Multitenancy.Features.v1.ResetTenantTheme;
using EDV.Modules.Multitenancy.Features.v1.TenantProvisioning.GetTenantProvisioningStatus;
using EDV.Modules.Multitenancy.Features.v1.TenantProvisioning.RetryTenantProvisioning;
using EDV.Modules.Multitenancy.Features.v1.UpdateTenantTheme;
using EDV.Modules.Multitenancy.Provisioning;
using EDV.Modules.Multitenancy.Services;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.EntityFrameworkCore.Stores;
using Finbuckle.MultiTenant.Extensions;
using Finbuckle.MultiTenant.Stores;
using Hangfire;
using Hangfire.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace EDV.Modules.Multitenancy;

public sealed class MultitenancyModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(
            MultitenancyPermissions.All);

        builder.Services.Configure<TenantBillingOptions>(
            builder.Configuration.GetSection(TenantBillingOptions.SectionName));

        builder.Services.AddScoped<ITenantService, TenantService>();
        builder.Services.AddScoped<ITenantThemeService, TenantThemeService>();
        builder.Services.AddTransient<IConnectionStringValidator, ConnectionStringValidator>();
        builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        builder.Services.AddTransient<TenantProvisioningJob>();
        builder.Services.AddTransient<TenantExpiryScanJob>();

        // Singleton — буфер переживает scope запроса, вызвавшего Store(...),
        // поэтому фоновый scope заполнения данными, запланированный через Hangfire, всё ещё может вызвать TryConsume(...).
        builder.Services.AddSingleton<
            ITenantInitialPasswordBuffer,
            Services.TenantInitialPasswordBuffer>();

        builder.Services.AddDbContext<TenantDbContext>();

        // Заменяем (а не добавляем) пустую реализацию scope тенанта для событий на реализацию на основе
        // Finbuckle, чтобы фоновая диспетчеризация событий устанавливала тенанта до построения
        // DbContext-ов обработчиков с фильтрацией по тенанту.
        builder.Services.Replace(
            ServiceDescriptor.Singleton<IEventTenantScope, FinbuckleEventTenantScope>());

        builder.Services
            .AddMultiTenant<AppTenantInfo>(options =>
            {
                options.Events.OnTenantResolveCompleted = async context =>
                {
                    if (context.MultiTenantContext.StoreInfo is null) return;
                    if (context.MultiTenantContext.StoreInfo.StoreType != typeof(DistributedCacheStore<AppTenantInfo>))
                    {
                        var sp = ((HttpContext)context.Context!).RequestServices;
                        var distributedStore = sp
                            .GetRequiredService<IEnumerable<IMultiTenantStore<AppTenantInfo>>>()
                            .FirstOrDefault(s => s.GetType() == typeof(DistributedCacheStore<AppTenantInfo>));

                        await distributedStore!.AddAsync(context.MultiTenantContext.TenantInfo!);
                    }
                    await Task.CompletedTask;
                };
            })
            // ── Цепочка стратегий — побеждает первый ненулевой идентификатор (в порядке регистрации) ──
            // ClaimStrategy здесь не срабатывает: UseMultiTenant() выполняется ДО UseAuthentication(), поэтому
            // User анонимен на момент разрешения. Тенант определяется через заголовок; переопределение для
            // корневого оператора — в middleware после аутентификации ниже.
            .WithClaimStrategy(ClaimConstants.Tenant)
            .WithHeaderStrategy(MultitenancyConstants.Identifier)
            .WithDelegateStrategy(async context =>
            {
                if (context is not HttpContext httpContext) return null;

                if (!httpContext.Request.Query.TryGetValue("tenant", out var tenantIdentifier) ||
                    string.IsNullOrEmpty(tenantIdentifier))
                    return null;

                return await Task.FromResult(tenantIdentifier.ToString());
            })
            .WithDistributedCacheStore(TimeSpan.FromMinutes(60))
            .WithStore<EFCoreStore<TenantDbContext, AppTenantInfo>>(ServiceLifetime.Scoped);

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<TenantDbContext>(
                name: "db:multitenancy",
                failureStatus: HealthStatus.Unhealthy)
            .AddCheck<TenantMigrationsHealthCheck>(
                name: "db:tenants-migrations",
                failureStatus: HealthStatus.Unhealthy);
    }

    public void ConfigureMiddleware(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // ── Переопределение заголовком для корневого оператора ──────────────────────────────
        // Вызывающий с claim'ом "root" может привязать один запрос к другому тенанту через заголовок `tenant`
        // (после аутентификации, поскольку в цепочке Finbuckle до аутентификации нет User). Условие: claim==root
        // + заголовок задан и != root + целевой тенант существует.
        app.Use(async (ctx, next) =>
        {
            var callerTenant = ctx.User?.FindFirstValue(ClaimConstants.Tenant);
            if (string.Equals(callerTenant, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
            {
                var headerValue = ctx.Request.Headers[MultitenancyConstants.Identifier].FirstOrDefault();
                if (!string.IsNullOrEmpty(headerValue) &&
                    !string.Equals(headerValue, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
                {
                    var store = ctx.RequestServices.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
                    var target = await store.GetAsync(headerValue).ConfigureAwait(false);
                    if (target is not null)
                    {
                        var setter = ctx.RequestServices.GetRequiredService<IMultiTenantContextSetter>();
                        setter.MultiTenantContext = new MultiTenantContext<AppTenantInfo>(target);
                    }
                }
            }
            await next(ctx).ConfigureAwait(false);
        });
        
#pragma warning disable S125
        // ── Защита от деактивированного тенанта ───────────────────────────────────
        // Finbuckle разрешает неактивных тенантов обычным образом, поэтому эта проверка после аутентификации
        // отклоняет любой запрос (включая анонимный логин/обновление токена) с некорневым неактивным тенантом.
        // Корневые операторы освобождены от проверки.
#pragma warning restore S125
        app.Use(async (ctx, next) =>
        {
            var callerTenant = ctx.User?.FindFirstValue(ClaimConstants.Tenant);
            var isOperator = string.Equals(callerTenant, MultitenancyConstants.Root.Id, StringComparison.Ordinal);
            if (!isOperator)
            {
                var accessor = ctx.RequestServices.GetRequiredService<IMultiTenantContextAccessor<AppTenantInfo>>();
                var tenant = accessor.MultiTenantContext?.TenantInfo;

                // ClaimStrategy не срабатывает до аутентификации, поэтому запрос только с JWT (без заголовка)
                // может не иметь разрешённого тенанта здесь — используем claim вызывающего в качестве запасного варианта.
                if (tenant is null && !string.IsNullOrEmpty(callerTenant))
                {
                    var store = ctx.RequestServices.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
                    tenant = await store.GetAsync(callerTenant).ConfigureAwait(false);
                }

                if (tenant is not null &&
                    !string.Equals(tenant.Id, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
                {
                    if (!tenant.IsActive)
                    {
                        throw new ForbiddenException("Этот тенант деактивирован. Обратитесь к администратору.");
                    }

                    // Истечение срока действия проверяется на каждом запросе (а не только при входе) с учётом
                    // льготного периода: тенант после ValidUpto продолжает работать до ValidUpto + льготный период,
                    // после чего жёстко блокируется.
                    var graceDays = ctx.RequestServices
                        .GetRequiredService<IOptions<TenantBillingOptions>>().Value.GracePeriodDays;
                    var nowUtc = ctx.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                    var graceEndsUtc = tenant.ValidUpto.AddDays(graceDays);
                    if (nowUtc > graceEndsUtc)
                    {
                        throw new ForbiddenException("Подписка этого тенанта истекла. Продлите её, чтобы продолжить.");
                    }

                    // Внутри льготного периода: сообщаем оставшееся количество дней, чтобы клиенты могли
                    // предупредить пользователя. Устанавливаем через OnStarting, чтобы заголовок сохранился,
                    // даже если обработчик исключений перезапишет ответ.
                    if (nowUtc > tenant.ValidUpto)
                    {
                        var daysLeft = (int)Math.Ceiling((graceEndsUtc - nowUtc).TotalDays);
                        var headerValue = daysLeft.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        ctx.Response.OnStarting(static state =>
                        {
                            var (response, value) = ((HttpResponse, string))state;
                            response.Headers["X-Subscription-Grace"] = value;
                            return Task.CompletedTask;
                        }, (ctx.Response, headerValue));
                    }
                }
            }

            await next(ctx).ConfigureAwait(false);
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var versionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints.MapGroup("api/v{version:apiVersion}/tenants")
            .WithTags("Tenants")
            .WithApiVersionSet(versionSet);
        ChangeTenantActivationEndpoint.Map(group);
        GetTenantsEndpoint.Map(group);
        RenewTenantEndpoint.Map(group);
        AdjustTenantValidityEndpoint.Map(group);
        CreateTenantEndpoint.Map(group);
        GetTenantStatusEndpoint.Map(group);
        GetMyTenantStatusEndpoint.Map(group);
        GetTenantProvisioningStatusEndpoint.Map(group);
        RetryTenantProvisioningEndpoint.Map(group);
        TenantMigrationsEndpoint.Map(group);

        // Эндпоинты темы
        GetTenantThemeEndpoint.Map(group);
        UpdateTenantThemeEndpoint.Map(group);
        ResetTenantThemeEndpoint.Map(group);

        var jobManager = endpoints.ServiceProvider.GetService<IRecurringJobManager>();
        if (jobManager is not null)
        {
            // Ежедневное сканирование тенантов в 02:00 UTC; публикует уведомления о скором истечении /
            // вхождении в льготный период / истечении срока действия.
            jobManager.AddOrUpdate(
                "tenant-expiry-scan",
                Job.FromExpression<TenantExpiryScanJob>(j => j.RunAsync(CancellationToken.None)),
                "0 2 * * *",
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
    }
}