using EDV.Framework.Eventing.Abstractions;

namespace EDV.Framework.Eventing.Outbox;

/// <summary>
/// Абстракция для хранения и чтения сообщений outbox.
/// </summary>
public interface IOutboxStore
{
    Task AddAsync(IIntegrationEvent @event, CancellationToken ct = default);

    Task<IReadOnlyList<OutboxMessage>> GetPendingBatchAsync(int batchSize, CancellationToken ct = default);

    Task MarkAsProcessedAsync(OutboxMessage message, CancellationToken ct = default);

    Task MarkAsFailedAsync(OutboxMessage message, string error, bool isDead, CancellationToken ct = default);

    /// <summary>Возвращает список мёртвых (исчерпавших попытки) сообщений, чтобы оператор мог их изучить.</summary>
    Task<IReadOnlyList<OutboxMessage>> GetDeadLetteredAsync(int max, CancellationToken ct = default);

    /// <summary>
    /// Сбрасывает мёртвые сообщения для новой попытки диспетчеризации (снимает флаг "мёртвое",
    /// счётчик повторов, последнюю ошибку и backoff). Передайте конкретные id, либо <c>null</c>,
    /// чтобы сбросить их все. Возвращает количество восстановленных сообщений.
    /// </summary>
    Task<int> RedriveDeadLettersAsync(IReadOnlyCollection<Guid>? ids, CancellationToken ct = default);
}
