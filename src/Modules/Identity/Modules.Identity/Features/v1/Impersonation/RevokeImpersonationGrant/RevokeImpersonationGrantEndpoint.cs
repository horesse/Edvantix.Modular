using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.v1.Impersonation;
using EDV.Modules.Identity.Contracts.v1.Impersonation.RevokeImpersonationGrant;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Impersonation.RevokeImpersonationGrant;

public static class RevokeImpersonationGrantEndpoint
{
    public sealed record Body(string? Reason);

    internal static RouteHandlerBuilder MapRevokeImpersonationGrantEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/impersonation/grants/{id:guid}/revoke",
            async (Guid id,
                   [FromBody] Body? body,
                   IMediator mediator,
                   CancellationToken ct) =>
                TypedResults.Ok(await mediator.Send(
                    new RevokeImpersonationGrantCommand(id, body?.Reason), ct)))
            .WithName("RevokeImpersonationGrant")
            .WithSummary("Отозвать грант имперсонализации")
            .WithDescription("Помечает грант как отозванный. Последующие запросы с токеном имперсонализации отклоняются хуком валидации JWT в течение ~1 секунды (TTL кэша).")
            .RequirePermission(IdentityPermissions.Impersonation.Revoke)
            .Produces<ImpersonationGrantDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}
