using EDV.Modules.Identity.Contracts.v1.TwoFactor;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.TwoFactor.Disable;

public sealed class DisableTwoFactorCommandValidator : AbstractValidator<DisableTwoFactorCommand>
{
    public DisableTwoFactorCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
    }
}
