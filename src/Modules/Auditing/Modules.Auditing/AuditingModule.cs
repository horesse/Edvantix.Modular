using Asp.Versioning;
using EDV.Framework.Persistence;
using EDV.Framework.Shared.Identity;
using EDV.Framework.Web.Modules;
using EDV.Modules.Auditing.Contracts;
using EDV.Modules.Auditing.Contracts.Authorization;
using EDV.Modules.Auditing.Core;
using EDV.Modules.Auditing.Features.v1.GetAuditById;
using EDV.Modules.Auditing.Features.v1.GetAudits;
using EDV.Modules.Auditing.Features.v1.GetAuditsByCorrelation;
using EDV.Modules.Auditing.Features.v1.GetAuditsByTrace;
using EDV.Modules.Auditing.Features.v1.GetAuditSummary;
using EDV.Modules.Auditing.Features.v1.GetExceptionAudits;
using EDV.Modules.Auditing.Features.v1.GetSecurityAudits;
using EDV.Modules.Auditing.Infrastructure.Http;
using EDV.Modules.Auditing.Infrastructure.Serialization;
using EDV.Modules.Auditing.Persistence;
using Hangfire;
using Hangfire.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace EDV.Modules.Auditing;

public class AuditingModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(
            AuditingPermissions.All);

        var httpOpts = builder.Configuration.GetSection("Auditing").Get<AuditHttpOptions>() ?? new AuditHttpOptions();
        builder.Services.AddSingleton(httpOpts);

        var retentionOpts = builder.Configuration.GetSection("Auditing:Retention").Get<AuditRetentionOptions>() ??
                            new AuditRetentionOptions();
        builder.Services.AddSingleton(retentionOpts);
        builder.Services.AddTransient<AuditRetentionJob>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IAuditClient, DefaultAuditClient>();
        builder.Services.AddScoped<ISecurityAudit, SecurityAudit>();
        builder.Services.AddDbContext<AuditDbContext>();
        builder.Services.AddScoped<IDbInitializer, AuditDbInitializer>();
        builder.Services.AddSingleton<IAuditSerializer, SystemTextJsonAuditSerializer>();
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<AuditDbContext>(
                name: "db:auditing",
                failureStatus: HealthStatus.Unhealthy);

        // Обогатители, используемые Audit.Configure (scoped, выполняются в потоке запроса)
        builder.Services.AddScoped<IAuditMaskingService, JsonMaskingService>();
        builder.Services.AddHostedService<AuditingConfigurator>();
        builder.Services.AddScoped<IAuditScope, HttpAuditScope>();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ChannelAuditPublisher>();
        builder.Services.AddSingleton<IAuditPublisher>(sp => sp.GetRequiredService<ChannelAuditPublisher>());
        builder.Services.AddScoped<ISaveChangesInterceptor, AuditingSaveChangesInterceptor>();

        builder.Services.AddSingleton<IAuditSink, SqlAuditSink>();
        builder.Services.AddSingleton<IAuditDlqSink, FileAuditDlqSink>();
        builder.Services.AddHostedService<AuditBackgroundWorker>();
    }

    public void ConfigureMiddleware(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseMiddleware<AuditHttpMiddleware>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var apiVersionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints
            .MapGroup("api/v{version:apiVersion}/audits")
            .WithTags("Audits")
            .WithApiVersionSet(apiVersionSet);

        group.MapGetAuditsEndpoint();
        group.MapGetAuditByIdEndpoint();
        group.MapGetAuditsByCorrelationEndpoint();
        group.MapGetAuditsByTraceEndpoint();
        group.MapGetSecurityAuditsEndpoint();
        group.MapGetExceptionAuditsEndpoint();
        group.MapGetAuditSummaryEndpoint();

        // Планируем очистку по хранению. Регистрация без условий безопасна — задание — no-op,
        // когда AuditRetentionOptions.Enabled равен false; операторы включают его в конфигурации.
        var jobManager = endpoints.ServiceProvider.GetService<IRecurringJobManager>();
        var retentionOpts = endpoints.ServiceProvider.GetService<AuditRetentionOptions>();
        if (jobManager is not null && retentionOpts is not null)
        {
            jobManager.AddOrUpdate(
                "auditing-retention",
                Job.FromExpression<AuditRetentionJob>(j => j.RunAsync(CancellationToken.None)),
                retentionOpts.Cron,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
    }
}