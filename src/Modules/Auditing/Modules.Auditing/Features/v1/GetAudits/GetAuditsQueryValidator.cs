using EDV.Framework.Web.Validation;
using EDV.Modules.Auditing.Contracts.v1.GetAudits;
using FluentValidation;

namespace EDV.Modules.Auditing.Features.v1.GetAudits;

public sealed class GetAuditsQueryValidator : AbstractValidator<GetAuditsQuery>
{
    public GetAuditsQueryValidator()
    {
        Include(new PagedQueryValidator<GetAuditsQuery>());

        RuleFor(q => q)
            .Must(q => !q.FromUtc.HasValue || !q.ToUtc.HasValue || q.FromUtc <= q.ToUtc)
            .WithMessage("FromUtc должно быть меньше или равно ToUtc.");

        // Отклоняем слишком большие окна заранее (пользователь видит 400, а не тихое ограничение).
        // Обработчик всё равно ограничивает окно как защиту в глубину (например, когда указана только одна граница).
        RuleFor(q => q)
            .Must(q =>
                !q.FromUtc.HasValue
                || !q.ToUtc.HasValue
                || (q.ToUtc.Value - q.FromUtc.Value) <= GetAuditsQueryHandler.MaxWindow)
            .WithMessage($"Окно запроса аудита не может превышать {GetAuditsQueryHandler.MaxWindow.TotalDays:0} дней.");
    }
}
