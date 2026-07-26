using Hangfire;
using Hangfire.Storage;

namespace EDV.Starter.Api;

/// <summary>
/// Одноразовое удаление осиротевших периодических заданий Hangfire вида <c>{module}-outbox-dispatcher</c> с максимальными усилиями.
/// Outbox теперь отправляется через <c>OutboxDispatcherHostedService</c> фреймворка (включён по умолчанию);
/// периодические задания Hangfire для каждого модуля были удалены (коммит 66130fc6), но Hangfire сохраняет
/// периодические задания в хранилище, поэтому развёртывания, созданные на старой версии, продолжают запускать их каждую минуту —
/// конкурируя с сервисом за те же строки и засоряя логи. Удаление кода не удалило сохранённое расписание;
/// этот сервис самовосстанавливает такие развёртывания при следующей загрузке. Безопасно оставлять: после очистки ничего не делает.
/// Запускается как <see cref="BackgroundService"/>, поэтому никогда не блокирует старт.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Создаётся DI через AddHostedService")]
internal sealed class OrphanedOutboxRecurringJobCleanupService : BackgroundService
{
    private const string OrphanSuffix = "-outbox-dispatcher";

    private readonly JobStorage _storage;
    private readonly IRecurringJobManager _recurringJobs;
    private readonly ILogger<OrphanedOutboxRecurringJobCleanupService> _logger;

    public OrphanedOutboxRecurringJobCleanupService(
        JobStorage storage,
        IRecurringJobManager recurringJobs,
        ILogger<OrphanedOutboxRecurringJobCleanupService> logger)
    {
        _storage = storage;
        _recurringJobs = recurringJobs;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Даём Hangfire завершить инициализацию схемы перед перечислением периодических заданий.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);

        try
        {
            using var connection = _storage.GetConnection();
            var orphanIds = connection.GetRecurringJobs()
                .Select(job => job.Id)
                .Where(id => id.EndsWith(OrphanSuffix, StringComparison.Ordinal))
                .ToList();

            foreach (var id in orphanIds)
            {
                _recurringJobs.RemoveIfExists(id);
                _logger.LogWarning(
                    "Удалено осиротевшее периодическое задание outbox {RecurringJobId}; outbox отправляется через OutboxDispatcherHostedService.",
                    id);
            }
        }
        // Очистка с максимальными усилиями: хранилище может быть ещё не готово при первом запуске или БД временно недоступна.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Не удалось удалить осиротевшие периодические задания outbox (хранилище Hangfire может быть ещё не готово).");
        }
    }
}