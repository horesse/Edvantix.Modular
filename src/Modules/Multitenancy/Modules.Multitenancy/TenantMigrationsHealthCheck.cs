using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Multitenancy.Data;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EDV.Modules.Multitenancy;

/// <summary>
/// Проверка готовности: возвращает <c>Unhealthy</c>, если у <c>TenantDbContext</c> любого тенанта есть
/// ожидающие миграции EF Core, либо если проверка миграций для конкретного тенанта выбрасывает исключение.
/// Подключена к <c>/health/ready</c>, на который ориентируются проверки готовности Kubernetes / балансировщика
/// нагрузки — поэтому под, чья схема отстаёт от работающей сборки, исключается из ротации, пока отдельный
/// <c>EDV.Starter.DbMigrator</c> не подтянет её до актуального состояния.
/// </summary>
public sealed class TenantMigrationsHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public TenantMigrationsHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();

        var tenantStore = scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var tenants = await tenantStore.GetAllAsync().ConfigureAwait(false);

        var details = new Dictionary<string, object>();
        var tenantsWithPending = new List<string>();
        var tenantsWithError = new List<string>();

        foreach (var tenant in tenants)
        {
            try
            {
                using IServiceScope tenantScope = scope.ServiceProvider.CreateScope();

                tenantScope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
                    .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

                var dbContext = tenantScope.ServiceProvider.GetRequiredService<TenantDbContext>();

                var pendingMigrations = (await dbContext.Database
                    .GetPendingMigrationsAsync(cancellationToken)
                    .ConfigureAwait(false))
                    .ToArray();

                bool hasPending = pendingMigrations.Length > 0;
                if (hasPending)
                {
                    tenantsWithPending.Add(tenant.Id!);
                }

                details[tenant.Id!] = new
                {
                    tenant.Name,
                    tenant.IsActive,
                    tenant.ValidUpto,
                    HasPendingMigrations = hasPending,
                    PendingMigrations = pendingMigrations
                };
            }
            // Проверки работоспособности должны сообщать об ошибках, а не выбрасывать исключения — фиксируем
            // сбои по каждому тенанту как записи деталей, чтобы полезная нагрузка проверки готовности
            // сообщала оператору, у какого тенанта проблема.
            catch (Exception ex)
            {
                tenantsWithError.Add(tenant.Id!);
                details[tenant.Id!] = new
                {
                    tenant.Name,
                    tenant.IsActive,
                    tenant.ValidUpto,
                    Error = ex.Message
                };
            }
        }

        if (tenantsWithError.Count > 0 || tenantsWithPending.Count > 0)
        {
            var description = BuildUnhealthyDescription(tenantsWithPending, tenantsWithError);
            return HealthCheckResult.Unhealthy(description, data: details);
        }

        return HealthCheckResult.Healthy("Все тенанты находятся на последней миграции.", details);
    }

    private static string BuildUnhealthyDescription(List<string> pending, List<string> errored)
    {
        var parts = new List<string>(2);
        if (pending.Count > 0)
        {
            parts.Add($"ожидающие миграции у тенанта(ов): {string.Join(", ", pending)}");
        }
        if (errored.Count > 0)
        {
            parts.Add($"ошибка проверки тенанта(ов): {string.Join(", ", errored)}");
        }
        return "Схема тенанта не актуальна — " + string.Join("; ", parts) +
               ". Запустите EDV.Starter.DbMigrator, чтобы применить ожидающие миграции.";
    }
}
