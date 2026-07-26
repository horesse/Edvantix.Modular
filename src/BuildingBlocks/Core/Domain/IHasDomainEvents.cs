namespace EDV.Framework.Core.Domain;

/// <summary>
/// Предоставляет доступ к доменным событиям, инициированным сущностью.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>
    /// Возвращает коллекцию инициированных доменных событий.
    /// </summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Очищает сохранённые доменные события.
    /// </summary>
    void ClearDomainEvents();
}