using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Multitenancy.Contracts.Authorization;
using EDV.Modules.Multitenancy.Contracts.Dtos;
using EDV.Modules.Multitenancy.Contracts.v1.TenantProvisioning;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Multitenancy.Features.v1.TenantProvisioning.RetryTenantProvisioning;

public static class RetryTenantProvisioningEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/{tenantId}/provisioning/retry", async (
            [FromRoute] string tenantId,
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new RetryTenantProvisioningCommand(tenantId), cancellationToken)))
            .WithName("RetryTenantProvisioning")
            .WithSummary("Повторить провижининг тенанта")
            .RequirePermission(MultitenancyPermissions.Tenants.Update)
            .WithDescription("Повторяет процесс провижининга для тенанта.")
            .Produces<TenantProvisioningStatusDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}