using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Identity.Claims;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Users.GetUserProfile;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace EDV.Modules.Identity.Features.v1.Users.GetUserProfile;

public static class GetUserProfileEndpoint
{
    internal static RouteHandlerBuilder MapGetMeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/profile", async (ClaimsPrincipal user, IMediator mediator, CancellationToken cancellationToken) =>
        {
            if (user.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedException();
            }

            return TypedResults.Ok(await mediator.Send(new GetCurrentUserProfileQuery(userId), cancellationToken));
        })
        .WithName("GetCurrentUserProfile")
        .WithSummary("Получить профиль текущего пользователя")
        .WithDescription("Возвращает профиль аутентифицированного пользователя из access-токена.")
        .RequireAuthorization()
        .Produces<UserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}