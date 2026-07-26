using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Multitenancy.Contracts;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Multitenancy.Provisioning;

public sealed class TenantProvisioningJob
{
    private readonly ITenantProvisioningService _provisioningService;
    private readonly IMultiTenantStore<AppTenantInfo> _tenantStore;
    private readonly IMultiTenantContextSetter _tenantContextSetter;
    private readonly ITenantService _tenantService;
    private readonly ILogger<TenantProvisioningJob> _logger;

    public TenantProvisioningJob(
        ITenantProvisioningService provisioningService,
        IMultiTenantStore<AppTenantInfo> tenantStore,
        IMultiTenantContextSetter tenantContextSetter,
        ITenantService tenantService,
        ILogger<TenantProvisioningJob> logger)
    {
        _provisioningService = provisioningService;
        _tenantStore = tenantStore;
        _tenantContextSetter = tenantContextSetter;
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task RunAsync(string tenantId, string correlationId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantStore.GetAsync(tenantId).ConfigureAwait(false)
            ?? throw new NotFoundException($"Тенант {tenantId} не найден при выполнении провижининга.");

        var currentStep = TenantProvisioningStepName.Database;
        try
        {
            var runDatabase = await _provisioningService.MarkRunningAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);

            _tenantContextSetter.MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

            if (runDatabase)
            {
                await _provisioningService.MarkStepCompletedAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            }

            currentStep = TenantProvisioningStepName.Migrations;
            var runMigrations = await _provisioningService.MarkRunningAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            if (runMigrations)
            {
                await _tenantService.MigrateTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
                await _provisioningService.MarkStepCompletedAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            }

            currentStep = TenantProvisioningStepName.Seeding;
            var runSeeding = await _provisioningService.MarkRunningAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            if (runSeeding)
            {
                await _tenantService.SeedTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
                await _provisioningService.MarkStepCompletedAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            }

            currentStep = TenantProvisioningStepName.CacheWarm;
            var runCacheWarm = await _provisioningService.MarkRunningAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            if (runCacheWarm)
            {
                await _provisioningService.MarkStepCompletedAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            }

            await _provisioningService.MarkCompletedAsync(tenantId, correlationId, cancellationToken).ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Тенант {TenantId} провижинирован, корреляция {CorrelationId}", tenantId, correlationId);
            }
        }
        // Широкий catch намеренный: любой сбой провижининга должен быть зафиксирован в журнале провижининга
        // перед повторным выбросом исключения, чтобы фреймворк заданий мог обработать повтор/dead-letter.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Провижининг тенанта {TenantId} завершился ошибкой", tenantId);
            await _provisioningService.MarkFailedAsync(tenantId, correlationId, currentStep, ex.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}