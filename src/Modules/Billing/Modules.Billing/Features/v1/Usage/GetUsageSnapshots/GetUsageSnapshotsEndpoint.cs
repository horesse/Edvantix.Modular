using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Billing.Contracts.Authorization;
using EDV.Modules.Billing.Contracts.v1.Usage;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Billing.Features.v1.Usage.GetUsageSnapshots;

public static class GetUsageSnapshotsEndpoint
{
    internal static RouteHandlerBuilder MapGetUsageSnapshotsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/usage",
                (string? tenantId, int? periodYear, int? periodMonth, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetUsageSnapshotsQuery(tenantId, periodYear, periodMonth), ct))
            .WithName("GetUsageSnapshots")
            .WithSummary("Список зафиксированных снимков использования")
            .RequirePermission(BillingPermissions.View);
    }
}
