using EDV.Framework.Web.Validation;
using EDV.Modules.Multitenancy.Contracts.v1.GetTenants;
using FluentValidation;

namespace EDV.Modules.Multitenancy.Features.v1.GetTenants;

public sealed class GetTenantsQueryValidator : AbstractValidator<GetTenantsQuery>
{
    public GetTenantsQueryValidator()
    {
        Include(new PagedQueryValidator<GetTenantsQuery>());
    }
}