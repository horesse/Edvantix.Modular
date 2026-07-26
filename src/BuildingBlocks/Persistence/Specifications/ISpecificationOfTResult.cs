using System.Linq.Expressions;

namespace EDV.Framework.Persistence.Specifications;

/// <summary>
/// Проецируемая спецификация, которая составляет запрос для <typeparamref name="T"/>
/// и затем выполняет проекцию в <typeparamref name="TResult"/>.
/// </summary>
/// <remarks>
/// Включения (Includes) могут игнорироваться при наличии селектора; поведение документировано
/// на уровне оценщика/расширений.
/// </remarks>
/// <typeparam name="T">Тип корневой сущности.</typeparam>
/// <typeparam name="TResult">Тип результата проекции.</typeparam>
public interface ISpecification<T, TResult> : ISpecification<T>
    where T : class
{
    /// <summary>
    /// Проекция, применяемая в конце конвейера запроса.
    /// </summary>
    Expression<Func<T, TResult>> Selector { get; }
}