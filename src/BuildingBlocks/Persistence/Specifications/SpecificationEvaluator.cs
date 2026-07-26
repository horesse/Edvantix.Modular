using Microsoft.EntityFrameworkCore;

namespace EDV.Framework.Persistence.Specifications;

/// <summary>
/// Внутренний оценщик, преобразующий спецификации в исполняемые запросы <see cref="IQueryable{T}"/>.
/// </summary>
internal static class SpecificationEvaluator
{
    /// <summary>
    /// Вычисляет спецификацию для входного запроса, создавая настроенный IQueryable.
    /// </summary>
    /// <typeparam name="T">Тип сущности.</typeparam>
    /// <param name="inputQuery">Базовый запрос, к которому применяется спецификация.</param>
    /// <param name="specification">Спецификация, содержащая конфигурацию запроса.</param>
    /// <returns>Настроенный запрос со всеми применёнными правилами спецификации.</returns>
    /// <exception cref="ArgumentNullException">Выбрасывается, когда inputQuery или specification равен null.</exception>
    public static IQueryable<T> GetQuery<T>(
        IQueryable<T> inputQuery,
        ISpecification<T> specification)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(inputQuery);
        ArgumentNullException.ThrowIfNull(specification);

        IQueryable<T> query = inputQuery;

        query = ApplyQueryBehaviors(query, specification);
        query = ApplyCriteria(query, specification);
        query = ApplyIncludes(query, specification);
        query = ApplyOrdering(query, specification);

        return query;
    }

    /// <summary>
    /// Вычисляет спецификацию с проекцией для входного запроса.
    /// </summary>
    /// <typeparam name="T">Тип сущности.</typeparam>
    /// <typeparam name="TResult">Тип результата проекции.</typeparam>
    /// <param name="inputQuery">Базовый запрос, к которому применяется спецификация.</param>
    /// <param name="specification">Спецификация, содержащая конфигурацию запроса и проекцию.</param>
    /// <returns>Настроенный запрос с применёнными правилами спецификации и проекцией.</returns>
    /// <exception cref="ArgumentNullException">Выбрасывается, когда inputQuery или specification равен null.</exception>
    public static IQueryable<TResult> GetQuery<T, TResult>(
        IQueryable<T> inputQuery,
        ISpecification<T, TResult> specification)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(inputQuery);
        ArgumentNullException.ThrowIfNull(specification);

        var query = GetQuery(inputQuery, (ISpecification<T>)specification);

        // Когда настроен селектор, включения (includes) могут игнорироваться на уровне EF,
        // но поведение последовательно применяется путём проекции в конце.
        return query.Select(specification.Selector);
    }

    private static IQueryable<T> ApplyQueryBehaviors<T>(IQueryable<T> query, ISpecification<T> specification)
        where T : class
    {
        if (specification.IgnoreQueryFilters)
        {
            query = query.IgnoreQueryFilters();
        }

        if (specification.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (specification.AsSplitQuery)
        {
            query = query.AsSplitQuery();
        }

        return query;
    }

    private static IQueryable<T> ApplyCriteria<T>(IQueryable<T> query, ISpecification<T> specification)
        where T : class
    {
        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        return query;
    }

    private static IQueryable<T> ApplyIncludes<T>(IQueryable<T> query, ISpecification<T> specification)
        where T : class
    {
        foreach (var include in specification.Includes)
        {
            query = query.Include(include);
        }

        foreach (var includeString in specification.IncludeStrings)
        {
            query = query.Include(includeString);
        }

        return query;
    }

    private static IQueryable<T> ApplyOrdering<T>(IQueryable<T> query, ISpecification<T> specification)
        where T : class
    {
        if (specification.OrderExpressions.Count == 0)
        {
            return query;
        }

        IOrderedQueryable<T>? ordered = null;

        foreach (var order in specification.OrderExpressions)
        {
            ordered = ApplyOrderExpression(query, ordered, order);
        }

        return ordered ?? query;
    }

    private static IOrderedQueryable<T> ApplyOrderExpression<T>(
        IQueryable<T> query,
        IOrderedQueryable<T>? ordered,
        OrderExpression<T> order)
        where T : class
    {
        if (ordered is null)
        {
            return order.Descending
                ? query.OrderByDescending(order.KeySelector)
                : query.OrderBy(order.KeySelector);
        }

        return order.Descending
            ? ordered.ThenByDescending(order.KeySelector)
            : ordered.ThenBy(order.KeySelector);
    }
}