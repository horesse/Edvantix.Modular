using EDV.Modules.Auditing.Contracts.v1.GetSecurityAudits;
using FluentValidation;

namespace EDV.Modules.Auditing.Features.v1.GetSecurityAudits;

public sealed class GetSecurityAuditsQueryValidator : AbstractValidator<GetSecurityAuditsQuery>
{
    public GetSecurityAuditsQueryValidator()
    {
        RuleFor(q => q)
            .Must(q => !q.FromUtc.HasValue || !q.ToUtc.HasValue || q.FromUtc <= q.ToUtc)
            .WithMessage("FromUtc должно быть меньше или равно ToUtc.");
    }
}