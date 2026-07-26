using EDV.Modules.Auditing.Contracts.v1.GetExceptionAudits;
using FluentValidation;

namespace EDV.Modules.Auditing.Features.v1.GetExceptionAudits;

public sealed class GetExceptionAuditsQueryValidator : AbstractValidator<GetExceptionAuditsQuery>
{
    public GetExceptionAuditsQueryValidator()
    {
        RuleFor(q => q)
            .Must(q => !q.FromUtc.HasValue || !q.ToUtc.HasValue || q.FromUtc <= q.ToUtc)
            .WithMessage("FromUtc должно быть меньше или равно ToUtc.");
    }
}