using EDV.Framework.Shared.Identity.Authorization;
using EDV.Framework.Web.Idempotency;
using EDV.Modules.Billing.Contracts.Authorization;
using EDV.Modules.Billing.Contracts.v1.Invoices;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Billing.Features.v1.Invoices.IssueInvoice;

public static class IssueInvoiceEndpoint
{
    public sealed record IssueInvoiceBody(DateTime? DueAtUtc);

    internal static RouteHandlerBuilder MapIssueInvoiceEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/invoices/{invoiceId:guid}/issue",
                async (Guid invoiceId, IssueInvoiceBody? body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new IssueInvoiceCommand(invoiceId, body?.DueAtUtc), ct)))
            .WithName("IssueInvoice")
            .WithSummary("Выставить черновик счёта")
            .RequirePermission(BillingPermissions.Manage)
            .WithIdempotency();
    }
}
