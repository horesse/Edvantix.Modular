using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Auditing.Contracts.Authorization;
using EDV.Modules.Auditing.Contracts.Dtos;
using EDV.Modules.Auditing.Contracts.v1.GetAuditsByTrace;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Auditing.Features.v1.GetAuditsByTrace;

public static class GetAuditsByTraceEndpoint
{
    public static RouteHandlerBuilder MapGetAuditsByTraceEndpoint(this IEndpointRouteBuilder group)
    {
        return group.MapGet(
                "/by-trace/{traceId}",
                async (string traceId, DateTime? fromUtc, DateTime? toUtc, IMediator mediator, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await mediator.Send(new GetAuditsByTraceQuery
                    {
                        TraceId = traceId,
                        FromUtc = fromUtc,
                        ToUtc = toUtc
                    }, cancellationToken)))
            .WithName("GetAuditsByTrace")
            .WithSummary("Получить события аудита по trace id")
            .WithDescription("Возвращает события аудита, связанные с указанным trace id.")
            .RequirePermission(AuditingPermissions.AuditTrails.View)
            .Produces<IEnumerable<AuditSummaryDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}