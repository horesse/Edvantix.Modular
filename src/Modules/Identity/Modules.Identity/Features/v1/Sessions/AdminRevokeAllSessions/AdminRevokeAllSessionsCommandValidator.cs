using EDV.Modules.Identity.Contracts.v1.Sessions.AdminRevokeAllSessions;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Sessions.AdminRevokeAllSessions;

public sealed class AdminRevokeAllSessionsCommandValidator : AbstractValidator<AdminRevokeAllSessionsCommand>
{
    public AdminRevokeAllSessionsCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Требуется ID пользователя.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Причина не должна превышать 500 символов.")
            .When(x => x.Reason is not null);
    }
}