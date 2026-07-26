using EDV.Modules.Identity.Contracts.v1.Sessions.RevokeSession;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Sessions.RevokeSession;

public sealed class RevokeSessionCommandValidator : AbstractValidator<RevokeSessionCommand>
{
    public RevokeSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Требуется ID сессии.");
    }
}