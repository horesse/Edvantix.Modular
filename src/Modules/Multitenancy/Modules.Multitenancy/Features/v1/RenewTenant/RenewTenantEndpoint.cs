using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Multitenancy.Contracts.Authorization;
using EDV.Modules.Multitenancy.Contracts.v1.RenewTenant;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Multitenancy.Features.v1.RenewTenant;

public static class RenewTenantEndpoint
{
    internal static RouteHandlerBuilder Map(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/{id}/renew", Handler)
            .WithName("RenewTenant")
            .WithSummary("Продлить подписку тенанта")
            .RequirePermission(MultitenancyPermissions.Tenants.UpgradeSubscription)
            .WithDescription("Продлевает срок действия тенанта на один срок плана, опционально меняя план, и выставляет счёт за период.")
            .Produces<RenewTenantCommandResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static async Task<Results<Ok<RenewTenantCommandResponse>, BadRequest>> Handler(
        string id,
        RenewTenantCommand command,
        IMediator dispatcher,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(id, command.TenantId, StringComparison.Ordinal))
        {
            return TypedResults.BadRequest();
        }

        var result = await dispatcher.Send(command, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(result);
    }
}
