using EDV.Modules.Multitenancy.Contracts.Dtos;
using Mediator;

namespace EDV.Modules.Multitenancy.Contracts.v1.TenantProvisioning;

public sealed record RetryTenantProvisioningCommand(string TenantId) : ICommand<TenantProvisioningStatusDto>;