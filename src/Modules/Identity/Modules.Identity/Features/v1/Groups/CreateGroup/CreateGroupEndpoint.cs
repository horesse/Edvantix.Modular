using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Groups.CreateGroup;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Groups.CreateGroup;

public static class CreateGroupEndpoint
{
    public static RouteHandlerBuilder MapCreateGroupEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/groups", async (IMediator mediator, [FromBody] CreateGroupCommand request, CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(request, cancellationToken);
            return TypedResults.Created($"/api/v1/groups/{result.Id}", result);
        })
        .WithName("CreateGroup")
        .WithSummary("Создать новую группу")
        .RequirePermission(IdentityPermissions.Groups.Create)
        .WithDescription("Создаёт новую группу с опциональными назначениями ролей.")
        .Produces<GroupDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status400BadRequest);
    }
}