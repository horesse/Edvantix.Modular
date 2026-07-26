using EDV.Modules.Identity.Contracts.v1.Users.AssignUserRoles;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Users.AssignUserRoles;

public sealed class AssignUserRolesCommandValidator : AbstractValidator<AssignUserRolesCommand>
{
    public AssignUserRolesCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Требуется ID пользователя.");

        RuleFor(x => x.UserRoles)
            .NotNull().WithMessage("Требуется список ролей пользователя.");
    }
}