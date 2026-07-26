using EDV.Framework.Shared.Identity;
using EDV.Framework.Shared.Multitenancy;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Identity.Authorization;

/// <summary>
/// Выполняется один раз при запуске хоста: проходит по каждому арендатору и добавляет все claims разрешений,
/// которые были зарегистрированы через <see cref="PermissionConstants"/>,
/// но отсутствуют в таблице claims ролей для этого арендатора. Идемпотентно и легковесно —
/// записывает только когда появляется что-то новое, поэтому безопасно выполнять безусловно.
/// </summary>
/// <remarks>
/// Реализован как <see cref="BackgroundService"/>, чтобы не блокировать запуск хоста.
/// В продакшене каталог арендаторов мигрируется отдельным консольным приложением
/// <c>EDV.Starter.DbMigrator</c> до запуска процесса API, поэтому хранилище арендаторов
/// уже заполнено при выполнении этого сервиса. Цикл опроса покрывает тестовые среды и
/// краткий промежуток во время локального запуска Aspire, когда миграция каталога может
/// перекрываться с другой работой при старте.
/// </remarks>
internal sealed class RolePermissionSyncHostedService(
    IServiceProvider serviceProvider,
    ILogger<RolePermissionSyncHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var tenants = await WaitForTenantsAsync(stoppingToken).ConfigureAwait(false);
            if (tenants is null)
            {
                logger.LogWarning(
                    "Синхронизация разрешений ролей пропущена — каталог арендаторов не был готов в течение {Timeout}",
                    MaxWait);
                return;
            }

            foreach (var tenant in tenants)
            {
                if (stoppingToken.IsCancellationRequested) break;
                await SyncTenantAsync(tenant, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Синхронизация разрешений выполняется с максимальными усилиями — никогда не падаем из-за неё.
            logger.LogError(ex, "Синхронизация разрешений ролей не удалась; новые разрешения могут быть недоступны до следующей синхронизации");
        }
    }

    /// <summary>
    /// Опрашивает хранилище арендаторов, пока арендаторы не станут доступны (каталог БД мигрирован и
    /// как минимум корневой арендатор создан). Возвращает null, если превышен дедлайн.
    /// </summary>
    private async Task<IEnumerable<AppTenantInfo>?> WaitForTenantsAsync(CancellationToken stoppingToken)
    {
        var deadline = DateTimeOffset.UtcNow + MaxWait;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (stoppingToken.IsCancellationRequested) return null;

            try
            {
                using var scope = serviceProvider.CreateScope();
                var tenantStore = scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
                var tenants = (await tenantStore.GetAllAsync().ConfigureAwait(false)).ToList();
                if (tenants.Count > 0)
                {
                    return tenants;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Каталог БД, вероятно, ещё не мигрирован — продолжаем ожидать.
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(ex, "Хранилище арендаторов ещё не готово; повтор через {Interval}", PollInterval);
                }
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task SyncTenantAsync(AppTenantInfo tenant, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
                .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

            var syncer = scope.ServiceProvider.GetRequiredService<RolePermissionSyncer>();
            await syncer.SyncAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ошибка для конкретного арендатора не должна останавливать остальной цикл.
            logger.LogError(ex, "Синхронизация разрешений ролей не удалась для арендатора '{Tenant}'", tenant.Id);
        }
    }
}