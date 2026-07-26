using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Billing.Contracts.Authorization;
using EDV.Modules.Billing.Contracts.v1.Wallets;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Billing.Features.v1.Wallets.GetMyWallet;

public static class GetMyWalletEndpoint
{
    internal static RouteHandlerBuilder MapGetMyWalletEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/wallet/me",
                async (IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new GetMyWalletQuery(), ct)))
            .WithName("GetMyWallet")
            .WithSummary("Получить кошелёк текущего тенанта")
            .RequirePermission(BillingPermissions.View);
    }
}
