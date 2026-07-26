using System.Linq.Expressions;

namespace EDV.Framework.Persistence.Specifications;

/// <summary>
/// Нормализованное представление выражения сортировки для спецификаций.
/// </summary>
/// <typeparam name="T">Тип корневой сущности.</typeparam>
public sealed record OrderExpression<T>(
    Expression<Func<T, object>> KeySelector,
    bool Descending)
    where T : class;