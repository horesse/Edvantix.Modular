using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Sessions.GetMySessions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Sessions.GetMySessions;

public static class GetMySessionsEndpoint
{
    internal static RouteHandlerBuilder MapGetMySessionsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/sessions/me", async (CancellationToken cancellationToken, IMediator mediator) =>
            TypedResults.Ok(await mediator.Send(new GetMySessionsQuery(), cancellationToken)))
        .WithName("GetMySessions")
        .WithSummary("Получить сессии текущего пользователя")
        .RequirePermission(IdentityPermissions.Sessions.View)
        .WithDescription("Возвращает все активные сессии текущего аутентифицированного пользователя.")
        .Produces<IEnumerable<UserSessionDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}