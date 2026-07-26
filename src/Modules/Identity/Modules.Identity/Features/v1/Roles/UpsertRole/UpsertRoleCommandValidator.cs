using EDV.Modules.Identity.Contracts.v1.Roles.UpsertRole;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Roles.UpsertRole;

public sealed class UpsertRoleCommandValidator : AbstractValidator<UpsertRoleCommand>
{
    public UpsertRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Требуется имя роли.");
    }
}