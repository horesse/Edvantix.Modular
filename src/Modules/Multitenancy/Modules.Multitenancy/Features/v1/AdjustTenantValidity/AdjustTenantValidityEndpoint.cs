using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Multitenancy.Contracts.Authorization;
using EDV.Modules.Multitenancy.Contracts.v1.AdjustTenantValidity;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Multitenancy.Features.v1.AdjustTenantValidity;

public static class AdjustTenantValidityEndpoint
{
    internal static RouteHandlerBuilder Map(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/{id}/adjust-validity", Handler)
            .WithName("AdjustTenantValidity")
            .WithSummary("Изменить срок действия тенанта (переопределение оператором)")
            .RequirePermission(MultitenancyPermissions.Tenants.UpgradeSubscription)
            .WithDescription("Устанавливает срок действия тенанта на явно заданную дату без выставления счёта или события продления — для бесплатных периодов, продления в рамках поддержки или немедленного истечения срока.")
            .Produces<AdjustTenantValidityCommandResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static async Task<Results<Ok<AdjustTenantValidityCommandResponse>, BadRequest>> Handler(
        string id,
        AdjustTenantValidityCommand command,
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
