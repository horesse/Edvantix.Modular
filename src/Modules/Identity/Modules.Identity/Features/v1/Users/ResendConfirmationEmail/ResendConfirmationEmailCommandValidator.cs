using EDV.Modules.Identity.Contracts.v1.Users.ResendConfirmationEmail;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Users.ResendConfirmationEmail;

public sealed class ResendConfirmationEmailCommandValidator : AbstractValidator<ResendConfirmationEmailCommand>
{
    public ResendConfirmationEmailCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Требуется ID пользователя.");
    }
}
