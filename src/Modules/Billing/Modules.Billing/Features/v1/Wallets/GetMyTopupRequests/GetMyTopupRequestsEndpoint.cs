using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Billing.Contracts;
using EDV.Modules.Billing.Contracts.Authorization;
using EDV.Modules.Billing.Contracts.v1.Wallets;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Billing.Features.v1.Wallets.GetMyTopupRequests;

public static class GetMyTopupRequestsEndpoint
{
    internal static RouteHandlerBuilder MapGetMyTopupRequestsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/wallet/topup-requests/me",
                (TopupRequestStatus? status, int pageNumber, int pageSize, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetMyTopupRequestsQuery(
                        status,
                        pageNumber <= 0 ? 1 : pageNumber,
                        pageSize <= 0 ? 20 : Math.Min(pageSize, 100)), ct))
            .WithName("GetMyTopupRequests")
            .WithSummary("Список запросов на пополнение текущего тенанта")
            .RequirePermission(BillingPermissions.View);
    }
}
