using EDV.Framework.Shared.Identity.Authorization;
using EDV.Framework.Shared.Persistence;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Users.SearchUsers;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Users.SearchUsers;

public static class SearchUsersEndpoint
{
    internal static RouteHandlerBuilder MapSearchUsersEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet(
                "/users/search",
                async ([AsParameters] SearchUsersQuery query, IMediator mediator, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await mediator.Send(query, cancellationToken)))
            .WithName("SearchUsers")
            .WithSummary("Поиск пользователей с пагинацией")
            .WithDescription("Ищет и фильтрует пользователей с серверной пагинацией, сортировкой и фильтрацией по статусу, подтверждению email и роли.")
            .RequirePermission(IdentityPermissions.Users.View)
            .Produces<PagedResponse<UserDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}