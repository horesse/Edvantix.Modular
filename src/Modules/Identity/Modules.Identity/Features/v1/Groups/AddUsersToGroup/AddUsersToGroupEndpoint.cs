using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.v1.Groups.AddUsersToGroup;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Groups.AddUsersToGroup;

public static class AddUsersToGroupEndpoint
{
    public static RouteHandlerBuilder MapAddUsersToGroupEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/groups/{groupId:guid}/members", async (Guid groupId, IMediator mediator, [FromBody] AddUsersRequest request, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new AddUsersToGroupCommand(groupId, request.UserIds), cancellationToken)))
        .WithName("AddUsersToGroup")
        .WithSummary("Добавить пользователей в группу")
        .RequirePermission(IdentityPermissions.Groups.ManageMembers)
        .WithDescription("Добавляет одного или нескольких пользователей в группу. Возвращает количество добавленных пользователей и список уже состоящих в группе.")
        .Produces<AddUsersToGroupResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest);
    }
}

public sealed record AddUsersRequest(IReadOnlyList<string> UserIds);