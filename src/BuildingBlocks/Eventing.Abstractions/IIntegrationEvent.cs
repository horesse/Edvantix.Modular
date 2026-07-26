namespace EDV.Framework.Eventing.Abstractions;

/// <summary>
/// Базовый контракт интеграционных событий, используемых для обмена сообщениями
/// между модулями и сервисами.
/// </summary>
public interface IIntegrationEvent
{
    Guid Id { get; }

    DateTime OccurredOnUtc { get; }

    /// <summary>
    /// Идентификатор арендатора для событий, ограниченных арендатором. Null для глобальных событий.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Идентификатор корреляции для привязки событий к запросам и трассировкам.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// Логический источник события (например, название модуля или сервиса).
    /// </summary>
    string Source { get; }
}