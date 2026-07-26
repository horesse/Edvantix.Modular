using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.v1.Sessions.RevokeSession;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Sessions.RevokeSession;

public static class RevokeSessionEndpoint
{
    internal static RouteHandlerBuilder MapRevokeSessionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/sessions/{sessionId:guid}", Handler)
        .WithName("RevokeSession")
        .WithSummary("Отозвать сессию")
        .RequirePermission(IdentityPermissions.Sessions.Revoke)
        .WithDescription("Отзывает конкретную сессию текущего аутентифицированного пользователя.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<NoContent, NotFound>> Handler(
        Guid sessionId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RevokeSessionCommand(sessionId), cancellationToken);
        return result ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}