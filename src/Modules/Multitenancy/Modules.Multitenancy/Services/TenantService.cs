using EDV.Framework.Core.Exceptions;
using EDV.Framework.Persistence;
using EDV.Framework.Persistence.Pagination;
using EDV.Framework.Persistence.Specifications;
using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Shared.Persistence;
using EDV.Modules.Multitenancy.Contracts;
using EDV.Modules.Multitenancy.Contracts.Dtos;
using EDV.Modules.Multitenancy.Contracts.v1.GetTenants;
using EDV.Modules.Multitenancy.Data;
using EDV.Modules.Multitenancy.Features.v1.GetTenants;
using EDV.Modules.Multitenancy.Provisioning;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EDV.Modules.Multitenancy.Services;

public sealed class TenantService : ITenantService
{
    private readonly IMultiTenantStore<AppTenantInfo> _tenantStore;
    private readonly DatabaseOptions _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly TenantDbContext _dbContext;
    private readonly ITenantProvisioningService _provisioningService;
    private readonly TimeProvider _timeProvider;
    private readonly TenantBillingOptions _billingOptions;
    private readonly ILogger<TenantService> _logger;

    public TenantService(
        IMultiTenantStore<AppTenantInfo> tenantStore,
        IOptions<DatabaseOptions> config,
        IServiceProvider serviceProvider,
        TenantDbContext dbContext,
        ITenantProvisioningService provisioningService,
        TimeProvider timeProvider,
        IOptions<TenantBillingOptions> billingOptions,
        ILogger<TenantService> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(billingOptions);
        _tenantStore = tenantStore;
        _config = config.Value;
        _serviceProvider = serviceProvider;
        _dbContext = dbContext;
        _provisioningService = provisioningService;
        _timeProvider = timeProvider;
        _billingOptions = billingOptions.Value;
        _logger = logger;
    }

    public async Task<string> ActivateAsync(string id, CancellationToken cancellationToken)
    {
        var tenant = await GetTenantInfoAsync(id, cancellationToken).ConfigureAwait(false);

        if (tenant.IsActive)
        {
            throw new CustomException($"Тенант {id} уже активирован");
        }

        await _provisioningService.EnsureCanActivateAsync(id, cancellationToken).ConfigureAwait(false);

        tenant.Activate();

        await _tenantStore.UpdateAsync(tenant).ConfigureAwait(false);
        await RefreshTenantCacheAsync(tenant).ConfigureAwait(false);

        return $"Тенант {id} теперь активирован";
    }

    public async Task<string> CreateAsync(string id,
        string name,
        string? connectionString,
        string adminEmail, string? issuer, string planKey, DateTime validUpto, CancellationToken cancellationToken)
    {
        if (connectionString?.Trim() == _config.ConnectionString.Trim())
        {
            connectionString = string.Empty;
        }

        AppTenantInfo tenant = new(id, name, connectionString, adminEmail, issuer)
        {
            Plan = planKey,
            // Устанавливаем ValidUpto напрямую по сроку плана: SetValidity() запрещает сдвигать дату назад,
            // а конструктор изначально задаёт now+1мес, поэтому он отклонил бы срок, вычисленный от более раннего 'now'.
            ValidUpto = DateTime.SpecifyKind(validUpto, DateTimeKind.Utc),
        };
        await _tenantStore.AddAsync(tenant).ConfigureAwait(false);
        await RefreshTenantCacheAsync(tenant).ConfigureAwait(false);

        return tenant.Id;
    }

    public async Task MigrateTenantAsync(AppTenantInfo tenant, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        foreach (var initializer in scope.ServiceProvider.GetServices<IDbInitializer>())
        {
            await initializer.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SeedTenantAsync(AppTenantInfo tenant, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        foreach (var initializer in scope.ServiceProvider.GetServices<IDbInitializer>())
        {
            await initializer.SeedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string> DeactivateAsync(string id, CancellationToken cancellationToken = default)
    {
        var tenant = await GetTenantInfoAsync(id, cancellationToken).ConfigureAwait(false);
        if (!tenant.IsActive)
        {
            throw new CustomException($"Тенант {id} уже деактивирован");
        }

        int tenantCount = (await _tenantStore.GetAllAsync().ConfigureAwait(false)).Count(t => t.IsActive);
        if (tenantCount <= 1)
        {
            throw new CustomException("Требуется хотя бы один активный тенант.");
        }

        if (tenant.Id.Equals(MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new CustomException("Корневой тенант нельзя деактивировать.");
        }

        tenant.Deactivate();
        await _tenantStore.UpdateAsync(tenant).ConfigureAwait(false);
        await RefreshTenantCacheAsync(tenant).ConfigureAwait(false);
        return $"Тенант {id} теперь деактивирован";
    }

    public async Task<bool> ExistsWithIdAsync(string id, CancellationToken cancellationToken = default) =>
        await _tenantStore.GetAsync(id).ConfigureAwait(false) is not null;

    public async Task<bool> ExistsWithNameAsync(string name, CancellationToken cancellationToken = default) =>
        (await _tenantStore.GetAllAsync().ConfigureAwait(false)).Any(t => t.Name == name);

    public async Task<PagedResponse<TenantDto>> GetAllAsync(GetTenantsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<AppTenantInfo> tenants = _dbContext.TenantInfo;
        var specification = new GetTenantsSpecification(query);
        IQueryable<TenantDto> projected = tenants.ApplySpecification(specification);

        return await projected
            .ToPagedResponseAsync(query, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TenantStatusDto> GetStatusAsync(string id, CancellationToken cancellationToken = default)
    {
        var tenant = await GetTenantInfoAsync(id, cancellationToken).ConfigureAwait(false);

        var graceEnds = tenant.ValidUpto.AddDays(_billingOptions.GracePeriodDays);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        string expiryState;
        if (now <= tenant.ValidUpto)
        {
            expiryState = "Active";
        }
        else if (now <= graceEnds)
        {
            expiryState = "InGrace";
        }
        else
        {
            expiryState = "Expired";
        }

        return new TenantStatusDto
        {
            Id = tenant.Id!,
            Name = tenant.Name!,
            IsActive = tenant.IsActive,
            ValidUpto = tenant.ValidUpto,
            HasConnectionString = !string.IsNullOrWhiteSpace(tenant.ConnectionString),
            AdminEmail = tenant.AdminEmail!,
            Issuer = tenant.Issuer,
            Plan = tenant.Plan,
            ExpiryState = expiryState,
            GraceEndsUtc = graceEnds
        };
    }

    public async Task<(DateTime PeriodStartUtc, DateTime ValidUpto, bool PlanChanged)> RenewAsync(
        string id, string newPlanKey, int termMonths, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPlanKey);

        var tenant = await GetTenantInfoAsync(id, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Складываем оставшееся время: продлеваем от ValidUpto, если он ещё в будущем, иначе от текущего момента.
        var periodStart = DateTime.SpecifyKind(tenant.ValidUpto > now ? tenant.ValidUpto : now, DateTimeKind.Utc);
        var newValidUpto = DateTime.SpecifyKind(periodStart.AddMonths(termMonths), DateTimeKind.Utc);
        var planChanged = !string.Equals(tenant.Plan, newPlanKey, StringComparison.OrdinalIgnoreCase);

        tenant.SetValidity(newValidUpto);
        if (planChanged)
        {
            tenant.Plan = newPlanKey;
        }

        await _tenantStore.UpdateAsync(tenant).ConfigureAwait(false);
        await RefreshTenantCacheAsync(tenant).ConfigureAwait(false);

        return (periodStart, newValidUpto, planChanged);
    }

    public async Task<DateTime> AdjustValidityAsync(string id, DateTime validUpto, CancellationToken cancellationToken = default)
    {
        var tenant = await GetTenantInfoAsync(id, cancellationToken).ConfigureAwait(false);

        // Устанавливаем напрямую, а не через SetValidity: это переопределение оператором может сдвигать дату
        // назад (например, немедленное истечение срока или исправление ошибки), что SetValidity запрещает.
        var normalized = DateTime.SpecifyKind(validUpto, DateTimeKind.Utc);
        var previous = tenant.ValidUpto;
        tenant.ValidUpto = normalized;

        await _tenantStore.UpdateAsync(tenant).ConfigureAwait(false);
        await RefreshTenantCacheAsync(tenant).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "[Multitenancy] оператор изменил срок действия тенанта {TenantId} с {Previous:o} на {ValidUpto:o}",
                id, previous, normalized);
        }

        return normalized;
    }

    private async Task<AppTenantInfo> GetTenantInfoAsync(string id, CancellationToken cancellationToken = default) =>
        await _tenantStore.GetAsync(id).ConfigureAwait(false)
            ?? throw new NotFoundException($"{typeof(AppTenantInfo).Name} {id} не найден.");

    // Finbuckle сначала разрешает через хранилище распределённого кэша (TTL 60 минут), а внедрённое хранилище
    // пишет только в EF, поэтому новое состояние нужно также протолкнуть в кэш-хранилище — иначе переключения
    // будут задерживаться до истечения кэша.
    private async Task RefreshTenantCacheAsync(AppTenantInfo tenant)
    {
        var cacheStore = _serviceProvider
            .GetServices<IMultiTenantStore<AppTenantInfo>>()
            .FirstOrDefault(s => s.GetType() == typeof(DistributedCacheStore<AppTenantInfo>));
        if (cacheStore is not null)
        {
            await cacheStore.UpdateAsync(tenant).ConfigureAwait(false);
        }
    }
}