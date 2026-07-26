using EDV.Framework.Shared.Identity.Authorization;
using EDV.Framework.Web.Idempotency;
using EDV.Modules.Billing.Contracts.Authorization;
using EDV.Modules.Billing.Contracts.v1.Wallets;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Billing.Features.v1.Wallets.RejectTopupRequest;

public static class RejectTopupRequestEndpoint
{
    public sealed record RejectTopupRequestBody(string? Reason);

    internal static RouteHandlerBuilder MapRejectTopupRequestEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/wallet/topup-requests/{id:guid}/reject",
                async (Guid id, RejectTopupRequestBody? body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new RejectTopupRequestCommand(id, body?.Reason), ct)))
            .WithName("RejectTopupRequest")
            .WithSummary("Отклонить ожидающий запрос на пополнение")
            .RequirePermission(BillingPermissions.Manage)
            .WithIdempotency();
    }
}
