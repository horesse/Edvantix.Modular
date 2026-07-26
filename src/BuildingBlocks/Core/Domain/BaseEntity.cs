namespace EDV.Framework.Core.Domain;

/// <summary>
/// Представляет базовую реализацию для сущностей с идентификатором и доменными событиями.
/// </summary>
/// <typeparam name="TId">Тип идентификатора сущности.</typeparam>
public abstract class BaseEntity<TId> : IEntity<TId>, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Возвращает идентификатор сущности.
    /// </summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>
    /// Возвращает доменные события, инициированные этой сущностью.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>
    /// Инициирует и записывает доменное событие для последующей отправки.
    /// </summary>
    /// <param name="event">Добавляемое доменное событие.</param>
    protected void AddDomainEvent(IDomainEvent @event)
        => _domainEvents.Add(@event);

    /// <summary>
    /// Очищает все записанные доменные события.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}