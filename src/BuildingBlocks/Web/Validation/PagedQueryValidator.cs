using EDV.Framework.Shared.Persistence;
using FluentValidation;

namespace EDV.Framework.Web.Validation;

/// <summary>
/// Общий валидатор для типов, реализующих IPagedQuery.
/// Используйте с Include() для добавления правил валидации пагинации в ваш валидатор.
/// </summary>
/// <example>
/// public class MyQueryValidator : AbstractValidator&lt;MyQuery&gt;
/// {
///     public MyQueryValidator()
///     {
///         Include(new PagedQueryValidator&lt;MyQuery&gt;());
///         // Добавьте дополнительные правила...
///     }
/// }
/// </example>
public sealed class PagedQueryValidator<T> : AbstractValidator<T>
    where T : IPagedQuery
{
    public PagedQueryValidator()
    {
        RuleFor(q => q.PageNumber)
            .GreaterThan(0)
            .When(q => q.PageNumber.HasValue)
            .WithMessage("Номер страницы должен быть больше 0.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100)
            .When(q => q.PageSize.HasValue)
            .WithMessage("Размер страницы должен быть от 1 до 100.");

        RuleFor(q => q.Sort)
            .MaximumLength(200)
            .When(q => !string.IsNullOrEmpty(q.Sort))
            .WithMessage("Выражение сортировки не должно превышать 200 символов.");
    }
}