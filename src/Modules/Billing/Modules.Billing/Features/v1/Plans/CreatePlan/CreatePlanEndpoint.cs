using EDV.Framework.Shared.Identity.Authorization;
using EDV.Framework.Web.Idempotency;
using EDV.Modules.Billing.Contracts.Authorization;
using EDV.Modules.Billing.Contracts.v1.Plans;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Billing.Features.v1.Plans.CreatePlan;

public static class CreatePlanEndpoint
{
    internal static RouteHandlerBuilder MapCreatePlanEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/plans",
                async (CreatePlanCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateBillingPlan")
            .WithSummary("Создать новый тариф")
            .RequirePermission(BillingPermissions.Manage)
            .WithIdempotency();
    }
}
