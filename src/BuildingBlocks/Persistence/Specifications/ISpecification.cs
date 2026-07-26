using System.Linq.Expressions;

namespace EDV.Framework.Persistence.Specifications;

/// <summary>
/// Спецификация на уровне сущности, описывающая способ построения запроса для <typeparamref name="T"/>.
/// Спецификации отвечают только за композицию запроса – никогда за пагинацию.
/// </summary>
/// <typeparam name="T">Тип корневой сущности.</typeparam>
public interface ISpecification<T>
    where T : class
{
    /// <summary>
    /// Необязательный критерий фильтрации, применяемый через <see cref="Queryable.Where{TSource}(IQueryable{TSource}, Expression{Func{TSource, bool}})"/>.
    /// </summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>
    /// Строго типизированные выражения для включения (Include).
    /// </summary>
    IReadOnlyList<Expression<Func<T, object>>> Includes { get; }

    /// <summary>
    /// Пути включения (Include) на основе строк.
    /// </summary>
    IReadOnlyList<string> IncludeStrings { get; }

    /// <summary>
    /// Выражения сортировки по умолчанию, применяемые, когда отсутствует переопределение сортировки на стороне клиента.
    /// </summary>
    IReadOnlyList<OrderExpression<T>> OrderExpressions { get; }

    /// <summary>
    /// Если true (по умолчанию), запросы выполняются с <c>AsNoTracking()</c>.
    /// </summary>
    bool AsNoTracking { get; }

    /// <summary>
    /// Если true, запросы выполняются с <c>AsSplitQuery()</c>.
    /// </summary>
    bool AsSplitQuery { get; }

    /// <summary>
    /// Если true, глобальные фильтры запросов EF Core игнорируются.
    /// </summary>
    bool IgnoreQueryFilters { get; }
}