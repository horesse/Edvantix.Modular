using EDV.Modules.Identity.Contracts.v1.Groups.RemoveUserFromGroup;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Groups.RemoveUserFromGroup;

public sealed class RemoveUserFromGroupCommandValidator : AbstractValidator<RemoveUserFromGroupCommand>
{
    public RemoveUserFromGroupCommandValidator()
    {
        RuleFor(x => x.GroupId)
            .NotEmpty().WithMessage("Требуется ID группы.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Требуется ID пользователя.");
    }
}