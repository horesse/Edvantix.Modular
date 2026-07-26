namespace EDV.Framework.Core.Domain;

/// <summary>
/// Представляет корень агрегата в доменной модели.
/// </summary>
/// <typeparam name="TId">Тип идентификатора агрегата.</typeparam>
public abstract class AggregateRoot<TId> : BaseEntity<TId>
{
    // При необходимости здесь можно разместить поведения/вспомогательные методы уровня агрегата
}