using EDV.Framework.Shared.Identity.Authorization;
using EDV.Framework.Shared.Persistence;
using EDV.Modules.Auditing.Contracts.Authorization;
using EDV.Modules.Auditing.Contracts.Dtos;
using EDV.Modules.Auditing.Contracts.v1.GetAudits;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Auditing.Features.v1.GetAudits;

public static class GetAuditsEndpoint
{
    public static RouteHandlerBuilder MapGetAuditsEndpoint(this IEndpointRouteBuilder group)
    {
        return group.MapGet(
                "/",
                async ([AsParameters] GetAuditsQuery query, IMediator mediator, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await mediator.Send(query, cancellationToken)))
            .WithName("GetAudits")
            .WithSummary("Список и поиск событий аудита")
            .WithDescription("Возвращает события аудита с пагинацией и фильтрами.")
            .RequirePermission(AuditingPermissions.AuditTrails.View)
            .Produces<PagedResponse<AuditSummaryDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}