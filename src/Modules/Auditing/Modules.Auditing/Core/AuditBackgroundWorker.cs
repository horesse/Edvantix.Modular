using EDV.Modules.Auditing.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading.Channels;

namespace EDV.Modules.Auditing.Core;

/// <summary>
/// Вычитывает канал и записывает в настроенный sink пакетами.
/// </summary>
public sealed class AuditBackgroundWorker : BackgroundService
{
    private readonly ChannelAuditPublisher _publisher;
    private readonly IAuditSink _sink;
    private readonly IAuditDlqSink _dlq;
    private readonly ILogger<AuditBackgroundWorker> _logger;

    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;

    /// <summary>Максимум попыток повтора основного sink на пакет перед DLQ.</summary>
    private const int MaxRetries = 3;

    /// <summary>Начальный backoff повтора. Удваивается на каждой попытке до потолка в 2с.</summary>
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(2);

    public AuditBackgroundWorker(
        ChannelAuditPublisher publisher,
        IAuditSink sink,
        IAuditDlqSink dlq,
        ILogger<AuditBackgroundWorker> logger,
        int batchSize = 200,
        int flushIntervalMs = 1000)
    {
        _publisher = publisher;
        _sink = sink;
        _dlq = dlq;
        _logger = logger;
        _batchSize = Math.Max(1, batchSize);
        _flushInterval = TimeSpan.FromMilliseconds(Math.Max(50, flushIntervalMs));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<AuditEnvelope>(_batchSize);
        var delayTask = Task.Delay(_flushInterval, stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var (shouldContinue, newDelayTask) = await ProcessBatchCycleAsync(batch, delayTask, stoppingToken);
                delayTask = newDelayTask;

                if (!shouldContinue)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Ожидаемо при штатном завершении работы
        }
        catch (Exception ex)
        {
            // Фоновый воркер не должен ронять хост — логируем и даём финальному flush шанс сохранить оставшиеся элементы
            _logger.LogError(ex, "Фоновый воркер аудита упал с исключением.");
        }

        await FinalFlushAsync(batch, stoppingToken);
    }

    private async Task<(bool shouldContinue, Task delayTask)> ProcessBatchCycleAsync(
        List<AuditEnvelope> batch,
        Task delayTask,
        CancellationToken stoppingToken)
    {
        // Полоса безопасности вычитывается первой, чтобы публикаторы под давлением разблокировались быстро,
        // затем полоса по умолчанию заполняет остаток, так что один flush амортизирует I/O обеих полос.
        DrainAvailableItems(_publisher.SecurityReader, batch);
        DrainAvailableItems(_publisher.Reader, batch);

        if (batch.Count >= _batchSize)
        {
            await FlushAsync(batch, stoppingToken);
            return (true, Task.Delay(_flushInterval, stoppingToken));
        }

        // Ждём, пока в одной из полос не появятся данные или не истечёт интервал flush. Каналы никогда
        // не закрываются (сигнализирует только stoppingToken), поэтому любое завершение, кроме delay, означает готовые данные.
        var securityWait = _publisher.SecurityReader.WaitToReadAsync(stoppingToken).AsTask();
        var defaultWait = _publisher.Reader.WaitToReadAsync(stoppingToken).AsTask();
        var winner = await Task.WhenAny(securityWait, defaultWait, delayTask);

        if (winner == securityWait || winner == defaultWait)
        {
            return (true, delayTask);
        }

        if (batch.Count > 0)
        {
            await FlushAsync(batch, stoppingToken);
        }

        return (true, Task.Delay(_flushInterval, stoppingToken));
    }

    private void DrainAvailableItems(ChannelReader<AuditEnvelope> reader, List<AuditEnvelope> batch)
    {
        while (batch.Count < _batchSize && reader.TryRead(out var item))
        {
            batch.Add(item);
        }
    }

    private async Task FinalFlushAsync(List<AuditEnvelope> batch, CancellationToken stoppingToken)
    {
        if (batch.Count > 0 && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FlushAsync(batch, stoppingToken).ConfigureAwait(false);
            }
            // Best-effort: сбой финального flush не должен всплывать во время завершения работы
            catch (Exception ex)
            {
                _logger.LogError(ex, "Финальный flush аудита завершился ошибкой.");
            }
        }
    }

    /// <summary>
    /// Пытается сбросить <paramref name="batch"/> через основной sink с ограниченным
    /// экспоненциальным backoff. При исчерпании попыток передаёт пакет в sink мёртвых
    /// писем, чтобы события пережили простой Postgres. Всегда очищает
    /// <paramref name="batch"/> перед возвратом, чтобы вызывающий код мог заполнить его заново.
    /// </summary>
    private async Task FlushAsync(List<AuditEnvelope> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        var sw = Stopwatch.StartNew();
        var snapshot = batch.ToArray();
        try
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await _sink.WriteAsync(snapshot, ct).ConfigureAwait(false);
                    AuditingTelemetry.Flushed.Add(snapshot.Length);
                    return;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Ожидаемо при завершении работы — не повторяем, не отправляем в DLQ. Элементы
                    // теряются при принудительном завершении; это приемлемо.
                    return;
                }
                catch (Exception ex)
                {
                    AuditingTelemetry.FlushFailed.Add(1,
                        new KeyValuePair<string, object?>("attempt", attempt));

                    if (attempt == MaxRetries)
                    {
                        _logger.LogError(ex, "Sink аудита не сработал после {Attempts} попыток; отправка {Count} событий в DLQ.",
                            attempt, snapshot.Length);
                        await _dlq.WriteAsync(snapshot, ct).ConfigureAwait(false);
                        return;
                    }

                    _logger.LogWarning(ex,
                        "Попытка flush sink аудита {Attempt}/{Max} завершилась ошибкой; повтор.",
                        attempt, MaxRetries);

                    var backoff = ComputeBackoff(attempt);
                    try { await Task.Delay(backoff, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                }
            }
        }
        finally
        {
            batch.Clear();
            AuditingTelemetry.FlushDurationMs.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    private static TimeSpan ComputeBackoff(int attempt)
    {
        var ms = Math.Min(
            MaxBackoff.TotalMilliseconds,
            InitialBackoff.TotalMilliseconds * Math.Pow(2, attempt - 1));
        return TimeSpan.FromMilliseconds(ms);
    }
}
