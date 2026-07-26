using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Auditing.Contracts.Authorization;
using EDV.Modules.Auditing.Contracts.Dtos;
using EDV.Modules.Auditing.Contracts.v1.GetSecurityAudits;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Auditing.Features.v1.GetSecurityAudits;

public static class GetSecurityAuditsEndpoint
{
    public static RouteHandlerBuilder MapGetSecurityAuditsEndpoint(this IEndpointRouteBuilder group)
    {
        return group.MapGet(
                "/security",
                async ([AsParameters] GetSecurityAuditsQuery query, IMediator mediator, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await mediator.Send(query, cancellationToken)))
            .WithName("GetSecurityAudits")
            .WithSummary("Получить события аудита безопасности")
            .WithDescription("Возвращает события аудита безопасности: вход, выход, отказы в доступе.")
            .RequirePermission(AuditingPermissions.AuditTrails.View)
            .Produces<IEnumerable<AuditSummaryDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}