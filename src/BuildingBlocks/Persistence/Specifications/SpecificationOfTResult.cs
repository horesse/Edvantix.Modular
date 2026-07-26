using System.Linq.Expressions;

namespace EDV.Framework.Persistence.Specifications;

/// <summary>
/// Базовая спецификация, которая составляет запрос для <typeparamref name="T"/> и
/// проецирует его в <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="T">Тип корневой сущности.</typeparam>
/// <typeparam name="TResult">Тип результата проекции.</typeparam>
public abstract class Specification<T, TResult> : Specification<T>, ISpecification<T, TResult>
    where T : class
{
    public Expression<Func<T, TResult>> Selector { get; private set; } = default!;

    /// <summary>
    /// Настраивает проекцию, применяемую в конце конвейера запроса.
    /// </summary>
    /// <param name="selector">Выражение проекции.</param>
    protected void Select(Expression<Func<T, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        Selector = selector;
    }
}