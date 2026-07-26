using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Billing.Contracts.Authorization;
using EDV.Modules.Billing.Contracts.v1.Plans;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Billing.Features.v1.Plans.GetPlans;

public static class GetPlansEndpoint
{
    internal static RouteHandlerBuilder MapGetPlansEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/plans",
                (bool includeInactive, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetPlansQuery(includeInactive), ct))
            .WithName("GetBillingPlans")
            .WithSummary("Список тарифов")
            .RequirePermission(BillingPermissions.View);
    }
}
