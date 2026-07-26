using EDV.Modules.Auditing.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Auditing.Persistence;

/// <summary>
/// Ежедневное задание Hangfire, которое очищает таблицу аудита согласно
/// <see cref="AuditRetentionOptions"/>. Использует <c>ExecuteDeleteAsync</c> с
/// ограниченным размером пакета, чтобы один прогон не держал долгую блокировку
/// на таблице — очистка по каждому типу события работает в цикле, пока не
/// будет удалено меньше строк, чем размер пакета.
/// </summary>
public sealed class AuditRetentionJob
{
    private readonly AuditDbContext _db;
    private readonly AuditRetentionOptions _opts;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuditRetentionJob> _logger;

    public AuditRetentionJob(
        AuditDbContext db,
        AuditRetentionOptions opts,
        TimeProvider timeProvider,
        ILogger<AuditRetentionJob> logger)
    {
        _db = db;
        _opts = opts;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        if (!_opts.Enabled)
        {
            _logger.LogInformation("[Auditing] задание очистки по хранению пропущено (Enabled=false).");
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        long total = 0;
        total += await SweepAsync(AuditEventType.Activity, now.AddDays(-_opts.ActivityRetentionDays), ct).ConfigureAwait(false);
        total += await SweepAsync(AuditEventType.EntityChange, now.AddDays(-_opts.EntityChangeRetentionDays), ct).ConfigureAwait(false);
        total += await SweepAsync(AuditEventType.Security, now.AddDays(-_opts.SecurityRetentionDays), ct).ConfigureAwait(false);
        total += await SweepAsync(AuditEventType.Exception, now.AddDays(-_opts.ExceptionRetentionDays), ct).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[Auditing] задание очистки по хранению удалило {Total} строк.", total);
        }
    }

    private async Task<long> SweepAsync(AuditEventType eventType, DateTime cutoffUtc, CancellationToken ct)
    {
        long swept = 0;
        var typeId = (int)eventType;
        var batchSize = Math.Max(100, _opts.DeleteBatchSize);

        while (!ct.IsCancellationRequested)
        {
            // Приём с подзапросом: ExecuteDeleteAsync не поддерживает TOP/LIMIT
            // напрямую, поэтому мы сначала фильтруем по ограниченному набору id.
            var deleted = await _db.AuditRecords
                .Where(a => a.EventType == typeId
                    && a.OccurredAtUtc < cutoffUtc
                    && _db.AuditRecords
                        .Where(b => b.EventType == typeId && b.OccurredAtUtc < cutoffUtc)
                        .OrderBy(b => b.OccurredAtUtc)
                        .Select(b => b.Id)
                        .Take(batchSize)
                        .Contains(a.Id))
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);

            swept += deleted;
            if (deleted < batchSize) break;
        }

        if (swept > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[Auditing] удалено {Count} событий {EventType} старше {Cutoff:o}.",
                swept, eventType, cutoffUtc);
        }
        return swept;
    }
}
