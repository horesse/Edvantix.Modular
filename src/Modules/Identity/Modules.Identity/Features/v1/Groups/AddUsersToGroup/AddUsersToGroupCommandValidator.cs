using EDV.Modules.Identity.Contracts.v1.Groups.AddUsersToGroup;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Groups.AddUsersToGroup;

public sealed class AddUsersToGroupCommandValidator : AbstractValidator<AddUsersToGroupCommand>
{
    public AddUsersToGroupCommandValidator()
    {
        RuleFor(x => x.GroupId)
            .NotEmpty().WithMessage("Требуется ID группы.");

        RuleFor(x => x.UserIds)
            .NotEmpty().WithMessage("Требуется хотя бы один ID пользователя.")
            .Must(ids => ids.All(id => !string.IsNullOrWhiteSpace(id)))
            .WithMessage("ID пользователей не могут быть пустыми или состоять только из пробелов.");
    }
}