using EDV.Modules.Identity.Contracts.v1.Roles.GetRoles;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Roles.GetRoles;

public sealed class GetRolesQueryValidator : AbstractValidator<GetRolesQuery>
{
    public GetRolesQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Номер страницы должен быть больше или равен 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage("Размер страницы должен быть больше или равен 1.");
    }
}
