using EDV.Modules.Identity.Contracts.v1.Users.AdminConfirmEmail;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Users.AdminConfirmEmail;

public sealed class AdminConfirmEmailCommandValidator : AbstractValidator<AdminConfirmEmailCommand>
{
    public AdminConfirmEmailCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Требуется ID пользователя.");
    }
}
