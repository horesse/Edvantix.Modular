using Mediator;

namespace EDV.Framework.Core.Domain;

/// <summary>
/// Представляет доменное событие с контекстом корреляции и арендатора.
/// Расширяет <see cref="INotification"/>, позволяя публиковать доменные события через Mediator.
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>
    /// Возвращает уникальный идентификатор события.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Возвращает временную метку UTC, когда произошло событие.
    /// </summary>
    DateTimeOffset OccurredOnUtc { get; }

    /// <summary>
    /// Возвращает идентификатор корреляции для отслеживания между границами.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Возвращает идентификатор арендатора, связанный с событием.
    /// </summary>
    string? TenantId { get; }
}