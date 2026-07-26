using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace EDV.Framework.Persistence.Specifications;

/// <summary>
/// Базовая спецификация для запросов на уровне сущностей.
/// </summary>
/// <typeparam name="T">Тип корневой сущности.</typeparam>
public abstract class Specification<T> : ISpecification<T>
    where T : class
{
    private readonly List<Expression<Func<T, bool>>> _criteria = [];
    private readonly List<Expression<Func<T, object>>> _includes = [];
    private readonly List<string> _includeStrings = [];
    private readonly List<OrderExpression<T>> _orderExpressions = [];

    protected Specification()
    {
        // По умолчанию предпочитаем запросы только для чтения.
        AsNoTracking = true;
    }

    public Expression<Func<T, bool>>? Criteria =>
        _criteria.Count == 0
            ? null
            : _criteria.Aggregate((current, next) => Combine(current, next));

    public IReadOnlyList<Expression<Func<T, object>>> Includes =>
        new ReadOnlyCollection<Expression<Func<T, object>>>(_includes);

    public IReadOnlyList<string> IncludeStrings =>
        new ReadOnlyCollection<string>(_includeStrings);

    public IReadOnlyList<OrderExpression<T>> OrderExpressions =>
        new ReadOnlyCollection<OrderExpression<T>>(_orderExpressions);

    public bool AsNoTracking { get; private set; }

    public bool AsSplitQuery { get; }

    public bool IgnoreQueryFilters { get; }

    /// <summary>
    /// Добавляет выражение критерия фильтрации, объединяемое с существующими критериями через логическое И.
    /// </summary>
    /// <param name="expression">Выражение фильтра.</param>
    protected void Where(Expression<Func<T, bool>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        _criteria.Add(expression);
    }

    /// <summary>
    /// Добавляет строго типизированное выражение включения (Include).
    /// </summary>
    protected void Include(Expression<Func<T, object>> includeExpression)
    {
        ArgumentNullException.ThrowIfNull(includeExpression);
        _includes.Add(includeExpression);
    }

    /// <summary>
    /// Добавляет путь включения (Include) на основе строки.
    /// </summary>
    protected void Include(string includeString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(includeString);
        _includeStrings.Add(includeString);
    }

    /// <summary>
    /// Настраивает первичную сортировку по возрастанию.
    /// </summary>
    protected void OrderBy(Expression<Func<T, object>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _orderExpressions.Add(new OrderExpression<T>(keySelector, Descending: false));
    }

    /// <summary>
    /// Настраивает первичную сортировку по убыванию.
    /// </summary>
    protected void OrderByDescending(Expression<Func<T, object>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _orderExpressions.Add(new OrderExpression<T>(keySelector, Descending: true));
    }

    /// <summary>
    /// Добавляет вторичную сортировку по возрастанию (ThenBy).
    /// </summary>
    protected void ThenBy(Expression<Func<T, object>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _orderExpressions.Add(new OrderExpression<T>(keySelector, Descending: false));
    }

    /// <summary>
    /// Добавляет вторичную сортировку по убыванию (ThenByDescending).
    /// </summary>
    protected void ThenByDescending(Expression<Func<T, object>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _orderExpressions.Add(new OrderExpression<T>(keySelector, Descending: true));
    }

    /// <summary>
    /// Очищает все настроенные выражения сортировки.
    /// </summary>
    protected void ClearOrderExpressions() => _orderExpressions.Clear();

    /// <summary>
    /// Включает <c>AsNoTracking()</c> для запроса (по умолчанию).
    /// </summary>
    protected void AsNoTrackingQuery() => AsNoTracking = true;

    /// <summary>
    /// Применяет выражение сортировки, предоставленное клиентом, используя белый список ключей сортировки.
    /// Когда предоставлено непустое выражение сортировки и разрешён хотя бы один допустимый ключ,
    /// сортировка клиента переопределяет любую существующую сортировку спецификации.
    /// Когда выражение сортировки пусто или содержит только недопустимые ключи, вызывается
    /// делегат <paramref name="applyDefaultOrdering"/> для настройки детерминированной
    /// сортировки по умолчанию.
    /// </summary>
    /// <param name="sortExpression">Выражение сортировки по нескольким колонкам, например: "Name,-CreatedOn".</param>
    /// <param name="applyDefaultOrdering">
    /// Делегат, настраивающий сортировку по умолчанию с использованием вспомогательных методов спецификации.
    /// </param>
    /// <param name="sortMappings">
    /// Сопоставление из белого списка ключей сортировки в строго типизированные выражения. Отражение (reflection) не используется.
    /// </param>
    protected void ApplySortingOverride(
        string? sortExpression,
        Action applyDefaultOrdering,
        IReadOnlyDictionary<string, Expression<Func<T, object>>> sortMappings)
    {
        ArgumentNullException.ThrowIfNull(applyDefaultOrdering);
        ArgumentNullException.ThrowIfNull(sortMappings);

        ClearOrderExpressions();

        if (string.IsNullOrWhiteSpace(sortExpression))
        {
            applyDefaultOrdering();
            return;
        }

        var clauses = ParseSortClauses(sortExpression);
        bool anyApplied = ApplySortClauses(clauses, sortMappings);

        if (!anyApplied)
        {
            applyDefaultOrdering();
        }
    }

    private static IEnumerable<string> ParseSortClauses(string sortExpression)
    {
        return sortExpression.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(clause => !string.IsNullOrWhiteSpace(clause));
    }

    private bool ApplySortClauses(IEnumerable<string> clauses, IReadOnlyDictionary<string, Expression<Func<T, object>>> sortMappings)
    {
        bool anyApplied = false;

        foreach (string rawClause in clauses)
        {
            var (key, descending) = ParseSortClause(rawClause);

            if (string.IsNullOrWhiteSpace(key) || !sortMappings.TryGetValue(key, out var selector))
            {
                continue;
            }

            ApplySortOrder(selector, descending, anyApplied);
            anyApplied = true;
        }

        return anyApplied;
    }

    private static (string key, bool descending) ParseSortClause(string clause)
    {
        clause = clause.Trim();
        bool descending = clause[0] == '-';
        string key = clause[0] is '-' or '+' ? clause[1..] : clause;
        return (key, descending);
    }

    private void ApplySortOrder(Expression<Func<T, object>> selector, bool descending, bool isSecondary)
    {
        if (isSecondary)
        {
            if (descending) ThenByDescending(selector);
            else ThenBy(selector);
        }
        else
        {
            if (descending) OrderByDescending(selector);
            else OrderBy(selector);
        }
    }

    private static Expression<Func<T, bool>> Combine(
        Expression<Func<T, bool>> first,
        Expression<Func<T, bool>> second)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var left = ReplaceParameter(first.Body, first.Parameters[0], parameter);
        var right = ReplaceParameter(second.Body, second.Parameters[0], parameter);
        var body = Expression.AndAlso(left, right);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static Expression ReplaceParameter(
        Expression expression,
        ParameterExpression source,
        ParameterExpression target)
    {
        return new ParameterReplaceVisitor(source, target).Visit(expression)
               ?? throw new InvalidOperationException("Не удалось заменить параметр в выражении.");
    }

    private sealed class ParameterReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        public ParameterReplaceVisitor(ParameterExpression source, ParameterExpression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }
}