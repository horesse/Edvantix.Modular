using EDV.Modules.Multitenancy.Contracts.v1.ChangeTenantActivation;
using FluentValidation;

namespace EDV.Modules.Multitenancy.Features.v1.ChangeTenantActivation;

internal sealed class ChangeTenantActivationCommandValidator : AbstractValidator<ChangeTenantActivationCommand>
{
    public ChangeTenantActivationCommandValidator() =>
       RuleFor(t => t.TenantId)
           .NotEmpty();
}