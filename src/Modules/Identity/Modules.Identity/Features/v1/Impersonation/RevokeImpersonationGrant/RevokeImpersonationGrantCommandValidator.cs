using EDV.Modules.Identity.Contracts.v1.Impersonation.RevokeImpersonationGrant;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Impersonation.RevokeImpersonationGrant;

public sealed class RevokeImpersonationGrantCommandValidator : AbstractValidator<RevokeImpersonationGrantCommand>
{
    public RevokeImpersonationGrantCommandValidator()
    {
        RuleFor(p => p.GrantId)
            .NotEmpty();

        RuleFor(p => p.Reason)
            .MaximumLength(512)
            .When(p => !string.IsNullOrEmpty(p.Reason));
    }
}
