using EDV.Modules.Multitenancy.Contracts.Dtos;
using EDV.Modules.Multitenancy.Contracts.v1.TenantProvisioning;
using EDV.Modules.Multitenancy.Provisioning;
using Mediator;

namespace EDV.Modules.Multitenancy.Features.v1.TenantProvisioning.RetryTenantProvisioning;

public sealed class RetryTenantProvisioningCommandHandler(ITenantProvisioningService provisioningService)
    : ICommandHandler<RetryTenantProvisioningCommand, TenantProvisioningStatusDto>
{
    public async ValueTask<TenantProvisioningStatusDto> Handle(RetryTenantProvisioningCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var correlationId = await provisioningService.RetryAsync(command.TenantId, cancellationToken).ConfigureAwait(false);
        var status = await provisioningService.GetStatusAsync(command.TenantId, cancellationToken).ConfigureAwait(false);
        return status with { CorrelationId = correlationId };
    }
}