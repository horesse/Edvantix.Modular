using EDV.Framework.Shared.Identity.Authorization;
using EDV.Framework.Web.Idempotency;
using EDV.Modules.Multitenancy.Contracts.Authorization;
using EDV.Modules.Multitenancy.Contracts.v1.CreateTenant;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Multitenancy.Features.v1.CreateTenant;

public static class CreateTenantEndpoint
{
    public static RouteHandlerBuilder Map(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/", async (
            [FromBody] CreateTenantCommand command,
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
            =>
            {
                var result = await mediator.Send(command, cancellationToken);
                return TypedResults.Created($"/api/v1/multitenancy/tenants/{result.Id}", result);
            })
            .WithName("CreateTenant")
            .WithSummary("Создать тенанта")
            .RequirePermission(MultitenancyPermissions.Tenants.Create)
            .WithIdempotency()
            .WithDescription("Создаёт нового тенанта.")
            .Produces<CreateTenantCommandResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status400BadRequest);
    }
}