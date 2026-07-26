using EDV.Modules.Identity.Contracts.v1.Users.ConfirmEmail;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Users.ConfirmEmail;

public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Требуется ID пользователя.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Требуется код подтверждения.");

        RuleFor(x => x.Tenant)
            .NotEmpty().WithMessage("Требуется арендатор.");
    }
}