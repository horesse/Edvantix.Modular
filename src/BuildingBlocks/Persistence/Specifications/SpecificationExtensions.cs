namespace EDV.Framework.Persistence.Specifications;

/// <summary>
/// Методы расширения для применения спецификаций к экземплярам <see cref="IQueryable{T}"/>.
/// </summary>
public static class SpecificationExtensions
{
    /// <summary>
    /// Применяет спецификацию на уровне сущности к запросу.
    /// </summary>
    public static IQueryable<T> ApplySpecification<T>(
        this IQueryable<T> query,
        ISpecification<T> specification)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        return SpecificationEvaluator.GetQuery(query, specification);
    }

    /// <summary>
    /// Применяет спецификацию с проекцией к запросу.
    /// </summary>
    public static IQueryable<TResult> ApplySpecification<T, TResult>(
        this IQueryable<T> query,
        ISpecification<T, TResult> specification)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        return SpecificationEvaluator.GetQuery(query, specification);
    }
}