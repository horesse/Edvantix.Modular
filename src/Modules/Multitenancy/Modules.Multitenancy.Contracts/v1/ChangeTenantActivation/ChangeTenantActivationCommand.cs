using EDV.Modules.Multitenancy.Contracts.Dtos;
using Mediator;

namespace EDV.Modules.Multitenancy.Contracts.v1.ChangeTenantActivation;

public sealed record ChangeTenantActivationCommand(string TenantId, bool IsActive)
    : ICommand<TenantLifecycleResultDto>;