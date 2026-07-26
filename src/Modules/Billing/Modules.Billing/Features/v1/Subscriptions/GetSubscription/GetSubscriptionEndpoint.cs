using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Billing.Contracts.Authorization;
using EDV.Modules.Billing.Contracts.v1.Subscriptions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Billing.Features.v1.Subscriptions.GetSubscription;

public static class GetSubscriptionEndpoint
{
    internal static RouteHandlerBuilder MapGetSubscriptionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/subscriptions",
                (string? tenantId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetSubscriptionQuery(tenantId), ct))
            .WithName("GetSubscription")
            .WithSummary("Получить активную подписку тенанта (администратор) или текущего тенанта")
            .RequirePermission(BillingPermissions.View);
    }

    internal static RouteHandlerBuilder MapGetMySubscriptionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/subscriptions/me",
                (IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetSubscriptionQuery(null), ct))
            .WithName("GetMySubscription")
            .WithSummary("Получить активную подписку текущего тенанта");
    }
}
