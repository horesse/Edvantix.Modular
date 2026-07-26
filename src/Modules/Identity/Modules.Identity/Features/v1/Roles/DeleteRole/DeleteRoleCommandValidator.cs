using EDV.Modules.Identity.Contracts.v1.Roles.DeleteRole;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Roles.DeleteRole;

public sealed class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Требуется ID роли.");
    }
}