using EDV.Framework.Shared.Identity.Authorization;
using EDV.Framework.Web.Idempotency;
using EDV.Modules.Billing.Contracts.Authorization;
using EDV.Modules.Billing.Contracts.v1.Invoices;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Billing.Features.v1.Invoices.MarkInvoicePaid;

public static class MarkInvoicePaidEndpoint
{
    internal static RouteHandlerBuilder MapMarkInvoicePaidEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/invoices/{invoiceId:guid}/pay",
                async (Guid invoiceId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new MarkInvoicePaidCommand(invoiceId), ct)))
            .WithName("MarkInvoicePaid")
            .WithSummary("Отметить выставленный счёт как оплаченный (вручную, без платёжного процессора)")
            .RequirePermission(BillingPermissions.Manage)
            .WithIdempotency();
    }
}
