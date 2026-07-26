using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.v1.Roles.UpdatePermissions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Roles.UpdateRolePermissions;

public static class UpdateRolePermissionsEndpoint
{
    public static RouteHandlerBuilder MapUpdateRolePermissionsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/{id}/permissions", Handler)
        .WithName("UpdateRolePermissions")
        .WithSummary("Обновить разрешения роли")
        .RequirePermission(IdentityPermissions.Roles.Update)
        .WithDescription("Заменяет набор разрешений, назначенных роли.")
        .Produces<string>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<string>, BadRequest>> Handler(
        string id,
        [FromBody] UpdatePermissionsCommand request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (id != request.RoleId)
        {
            return TypedResults.BadRequest();
        }

        var response = await mediator.Send(request, cancellationToken);
        return TypedResults.Ok(response);
    }
}