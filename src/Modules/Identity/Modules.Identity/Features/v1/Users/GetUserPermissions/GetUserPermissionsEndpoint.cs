using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Identity.Claims;
using EDV.Modules.Identity.Contracts.v1.Users.GetUserPermissions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace EDV.Modules.Identity.Features.v1.Users.GetUserPermissions;

public static class GetUserPermissionsEndpoint
{
    // Намеренно без RequirePermission: возвращает разрешения *самого вызывающего* (они нужны SPA для
    // рендеринга защищённых маршрутов); ограничение через Users.View заблокировало бы роли, не управляющие
    // пользователями. Резервная политика → 401.
    internal static RouteHandlerBuilder MapGetCurrentUserPermissionsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/permissions", async (ClaimsPrincipal user, IMediator mediator, CancellationToken cancellationToken) =>
        {
            if (user.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedException();
            }

            return TypedResults.Ok(await mediator.Send(new GetCurrentUserPermissionsQuery(userId), cancellationToken));
        })
        .WithName("GetCurrentUserPermissions")
        .WithSummary("Получить разрешения текущего пользователя")
        .WithDescription("Возвращает разрешения аутентифицированного пользователя. Требует только аутентификации — каждый вошедший пользователь может прочитать свои собственные права.")
        .Produces<IEnumerable<string>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}