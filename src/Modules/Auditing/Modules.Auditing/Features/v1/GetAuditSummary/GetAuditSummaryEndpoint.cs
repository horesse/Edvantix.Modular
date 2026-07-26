using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Auditing.Contracts.Authorization;
using EDV.Modules.Auditing.Contracts.Dtos;
using EDV.Modules.Auditing.Contracts.v1.GetAuditSummary;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Auditing.Features.v1.GetAuditSummary;

public static class GetAuditSummaryEndpoint
{
    public static RouteHandlerBuilder MapGetAuditSummaryEndpoint(this IEndpointRouteBuilder group)
    {
        return group.MapGet(
                "/summary",
                async ([AsParameters] GetAuditSummaryQuery query, IMediator mediator, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await mediator.Send(query, cancellationToken)))
            .WithName("GetAuditSummary")
            .WithSummary("Получить сводку аудита")
            .WithDescription("Возвращает агрегированные счётчики событий аудита по типу, серьёзности, источнику и арендатору.")
            .RequirePermission(AuditingPermissions.AuditTrails.View)
            .Produces<AuditSummaryAggregateDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}