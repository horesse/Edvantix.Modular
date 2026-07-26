using EDV.Modules.Auditing.Contracts.v1.GetAuditSummary;
using FluentValidation;

namespace EDV.Modules.Auditing.Features.v1.GetAuditSummary;

public sealed class GetAuditSummaryQueryValidator : AbstractValidator<GetAuditSummaryQuery>
{
    public GetAuditSummaryQueryValidator()
    {
        RuleFor(q => q)
            .Must(q => !q.FromUtc.HasValue || !q.ToUtc.HasValue || q.FromUtc <= q.ToUtc)
            .WithMessage("FromUtc должно быть меньше или равно ToUtc.");

        RuleFor(q => q)
            .Must(q =>
                !q.FromUtc.HasValue
                || !q.ToUtc.HasValue
                || (q.ToUtc.Value - q.FromUtc.Value) <= GetAuditSummaryQueryHandler.MaxWindow)
            .WithMessage($"Окно сводки аудита не может превышать {GetAuditSummaryQueryHandler.MaxWindow.TotalDays:0} дней.");
    }
}
