namespace EDV.Framework.Eventing.Inbox;

/// <summary>
/// Абстракция для отслеживания идемпотентных потребителей.
/// </summary>
public interface IInboxStore
{
    Task<bool> HasProcessedAsync(Guid eventId, string handlerName, CancellationToken ct = default);

    Task MarkProcessedAsync(Guid eventId, string handlerName, string? tenantId, string eventType, CancellationToken ct = default);
}
