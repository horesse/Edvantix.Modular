using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Billing.Contracts.Authorization;
using EDV.Modules.Billing.Contracts.v1.Invoices;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Billing.Features.v1.Invoices.GetInvoiceById;

public static class GetInvoiceByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetInvoiceByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/invoices/{invoiceId:guid}",
                (Guid invoiceId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetInvoiceByIdQuery(invoiceId), ct))
            .WithName("GetInvoiceById")
            .WithSummary("Получить один счёт по идентификатору")
            .RequirePermission(BillingPermissions.View);
    }
}
