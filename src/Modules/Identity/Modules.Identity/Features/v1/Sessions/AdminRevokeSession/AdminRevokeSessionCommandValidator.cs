using EDV.Modules.Identity.Contracts.v1.Sessions.AdminRevokeSession;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Sessions.AdminRevokeSession;

public sealed class AdminRevokeSessionCommandValidator : AbstractValidator<AdminRevokeSessionCommand>
{
    public AdminRevokeSessionCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Требуется ID пользователя.");

        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Требуется ID сессии.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Причина не должна превышать 500 символов.")
            .When(x => x.Reason is not null);
    }
}