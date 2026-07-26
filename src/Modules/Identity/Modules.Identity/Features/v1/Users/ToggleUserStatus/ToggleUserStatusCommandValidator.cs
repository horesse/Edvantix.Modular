using EDV.Modules.Identity.Contracts.v1.Users.ToggleUserStatus;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Users.ToggleUserStatus;

public sealed class ToggleUserStatusCommandValidator : AbstractValidator<ToggleUserStatusCommand>
{
    public ToggleUserStatusCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Требуется ID пользователя.");
    }
}