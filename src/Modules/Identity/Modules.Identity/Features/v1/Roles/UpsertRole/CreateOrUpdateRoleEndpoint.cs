using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Roles.UpsertRole;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Roles.UpsertRole;

public static class CreateOrUpdateRoleEndpoint
{
    public static RouteHandlerBuilder MapCreateOrUpdateRoleEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/roles", async (IMediator mediator, [FromBody] UpsertRoleCommand request, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(request, cancellationToken)))
        .WithName("CreateOrUpdateRole")
        .WithSummary("Создать или обновить роль")
        .RequirePermission(IdentityPermissions.Roles.Create)
        .WithDescription("Создаёт новую роль либо обновляет имя и описание существующей.")
        .Produces<RoleDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}