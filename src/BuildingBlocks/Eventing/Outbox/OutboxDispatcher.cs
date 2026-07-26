using EDV.Framework.Eventing.Abstractions;
using EDV.Framework.Eventing.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EDV.Framework.Eventing.Outbox;

/// <summary>
/// Диспетчеризует сообщения outbox через настроенную шину событий.
/// Этот тип предназначен для вызова планировщиком (например, периодическим заданием Hangfire
/// или хостед-сервисом).
/// </summary>
public sealed partial class OutboxDispatcher
{
    private readonly IOutboxStore _outbox;
    private readonly IEventBus _bus;
    private readonly IEventSerializer _serializer;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly EventingOptions _options;

    public OutboxDispatcher(
        IOutboxStore outbox,
        IEventBus bus,
        IEventSerializer serializer,
        IOptions<EventingOptions> options,
        ILogger<OutboxDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _outbox = outbox;
        _bus = bus;
        _serializer = serializer;
        _logger = logger;
        _options = options.Value;
    }

    public async Task DispatchAsync(CancellationToken ct = default)
    {
        var batchSize = _options.OutboxBatchSize;
        if (batchSize <= 0) batchSize = 100;

        var messages = await _outbox.GetPendingBatchAsync(batchSize, ct).ConfigureAwait(false);
        if (messages.Count == 0)
        {
            _logger.LogDebug("Нет сообщений outbox для диспетчеризации.");
            return;
        }

        LogDispatching(messages.Count, batchSize);

        var processedCount = 0;
        var failedCount = 0;
        var deadLetterCount = 0;

        foreach (var message in messages)
        {
            try
            {
                var @event = _serializer.Deserialize(message.Payload, message.Type);
                if (@event is null)
                {
                    await _outbox.MarkAsFailedAsync(message, "Не удалось десериализовать интеграционное событие.", isDead: true, ct).ConfigureAwait(false);
                    continue;
                }

                await _bus.PublishAsync(@event, ct).ConfigureAwait(false);
                await _outbox.MarkAsProcessedAsync(message, ct).ConfigureAwait(false);
                processedCount++;

                LogMessageDispatched(message.Id);
            }
            // Широкий catch намеренный: каждое сообщение должно обрабатываться независимо,
            // и любой тип сбоя должен запускать механизм повтора/мёртвых сообщений.
            catch (Exception ex)
            {
                var maxRetries = _options.OutboxMaxRetries <= 0 ? 5 : _options.OutboxMaxRetries;
                var isDead = message.RetryCount + 1 >= maxRetries;

                await _outbox.MarkAsFailedAsync(message, ex.Message, isDead, ct).ConfigureAwait(false);

                failedCount++;
                if (isDead)
                {
                    deadLetterCount++;
                    EventingTelemetry.OutboxDeadLettered.Add(1);
                }

                if (isDead)
                {
                    _logger.LogError(ex, "Сообщение outbox {MessageId} перемещено в мёртвые письма после {RetryCount} повторов", message.Id, message.RetryCount + 1);
                }
                else
                {
                    _logger.LogWarning(ex, "Сбой сообщения outbox {MessageId} (RetryCount={RetryCount}).", message.Id, message.RetryCount + 1);
                }
            }
        }

        LogDispatchSummary(messages.Count, processedCount, failedCount, deadLetterCount);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Диспетчеризация {Count} сообщений outbox (BatchSize={BatchSize})")]
    private partial void LogDispatching(int count, int batchSize);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Сообщение outbox {MessageId} доставлено и помечено как обработанное.")]
    private partial void LogMessageDispatched(Guid messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Итоги диспетчеризации outbox: Всего={Total}, Обработано={Processed}, Сбоев={Failed}, МёртвыхПисем={DeadLettered}")]
    private partial void LogDispatchSummary(int total, int processed, int failed, int deadLettered);
}
