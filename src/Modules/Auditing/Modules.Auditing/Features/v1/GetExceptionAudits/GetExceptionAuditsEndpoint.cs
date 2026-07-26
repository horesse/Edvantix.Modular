using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Auditing.Contracts.Authorization;
using EDV.Modules.Auditing.Contracts.Dtos;
using EDV.Modules.Auditing.Contracts.v1.GetExceptionAudits;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Auditing.Features.v1.GetExceptionAudits;

public static class GetExceptionAuditsEndpoint
{
    public static RouteHandlerBuilder MapGetExceptionAuditsEndpoint(this IEndpointRouteBuilder group)
    {
        return group.MapGet(
                "/exceptions",
                async ([AsParameters] GetExceptionAuditsQuery query, IMediator mediator, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await mediator.Send(query, cancellationToken)))
            .WithName("GetExceptionAudits")
            .WithSummary("Получить события аудита исключений")
            .WithDescription("Возвращает события аудита, связанные с исключениями.")
            .RequirePermission(AuditingPermissions.AuditTrails.View)
            .Produces<IEnumerable<AuditSummaryDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}