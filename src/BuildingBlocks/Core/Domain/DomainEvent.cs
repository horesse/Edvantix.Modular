namespace EDV.Framework.Core.Domain;

/// <summary>
/// Базовое доменное событие с контекстом корреляции и арендатора.
/// </summary>
/// <param name="EventId">Уникальный идентификатор события.</param>
/// <param name="OccurredOnUtc">Временная метка UTC, когда произошло событие.</param>
/// <param name="CorrelationId">Необязательный идентификатор корреляции.</param>
/// <param name="TenantId">Необязательный идентификатор арендатора.</param>
public abstract record DomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    string? CorrelationId = null,
    string? TenantId = null
) : IDomainEvent
{
    /// <summary>
    /// Создаёт новое доменное событие с использованием указанной фабрики.
    /// </summary>
    /// <typeparam name="T">Тип создаваемого доменного события.</typeparam>
    /// <param name="factory">Фабрика для создания события с использованием сгенерированного идентификатора и временной метки.</param>
    /// <returns>Созданное доменное событие.</returns>
    public static T Create<T>(Func<Guid, DateTimeOffset, T> factory)
        where T : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(factory);
        return factory(Guid.NewGuid(), DateTimeOffset.UtcNow);
    }
}