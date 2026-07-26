namespace EDV.Framework.Core.Domain;

/// <summary>
/// Представляет сущность со строго типизированным идентификатором.
/// </summary>
/// <typeparam name="TId">Тип идентификатора сущности.</typeparam>
public interface IEntity<out TId>
{
    /// <summary>
    /// Возвращает идентификатор сущности.
    /// </summary>
    TId Id { get; }
}