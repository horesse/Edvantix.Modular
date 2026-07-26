using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Users.GetUsers;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Users.GetUsers;

public static class GetUsersListEndpoint
{
    internal static RouteHandlerBuilder MapGetUsersListEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/users", async (CancellationToken cancellationToken, IMediator mediator) =>
            TypedResults.Ok(await mediator.Send(new GetUsersQuery(), cancellationToken)))
        .WithName("ListUsers")
        .WithSummary("Список пользователей")
        .RequirePermission(IdentityPermissions.Users.View)
        .WithDescription("Возвращает список пользователей текущего арендатора.")
        .Produces<IEnumerable<UserDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}