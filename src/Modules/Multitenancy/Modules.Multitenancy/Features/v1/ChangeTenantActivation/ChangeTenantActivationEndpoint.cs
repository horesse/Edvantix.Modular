using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Multitenancy.Contracts.Authorization;
using EDV.Modules.Multitenancy.Contracts.Dtos;
using EDV.Modules.Multitenancy.Contracts.v1.ChangeTenantActivation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Multitenancy.Features.v1.ChangeTenantActivation;

public static class ChangeTenantActivationEndpoint
{
    public static RouteHandlerBuilder Map(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/{id}/activation", Handler)
            .WithName("ChangeTenantActivation")
            .WithSummary("Изменить состояние активации тенанта")
            .WithDescription("Активирует или деактивирует тенанта через единый эндпоинт.")
            .RequirePermission(MultitenancyPermissions.Tenants.Update)
            .Produces<TenantLifecycleResultDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<TenantLifecycleResultDto>, BadRequest>> Handler(
        [FromRoute] string id,
        [FromBody] ChangeTenantActivationCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(id, command.TenantId, StringComparison.Ordinal))
        {
            return TypedResults.BadRequest();
        }

        TenantLifecycleResultDto result = await mediator.Send(command, cancellationToken);
        return TypedResults.Ok(result);
    }
}