using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Billing.Contracts.Authorization;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Billing.Features.v1.Invoices.GetInvoicePdf;

public static class GetInvoicePdfEndpoint
{
    internal static RouteHandlerBuilder MapGetInvoicePdfEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/invoices/{invoiceId:guid}/pdf",
                async (Guid invoiceId, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new GetInvoicePdfQuery(invoiceId), ct).ConfigureAwait(false);
                    return Results.File(result.Content, "application/pdf", result.FileName);
                })
            .WithName("GetInvoicePdf")
            .WithSummary("Скачать счёт в формате PDF")
            // BillingPermissions.View — базовое право (выдаётся пользователям тенанта), а обработчик
            // ограничивает область тенантом вызывающего — поэтому один и тот же эндпоинт обслуживает
            // как операторов, так и самообслуживание тенанта.
            .RequirePermission(BillingPermissions.View)
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces(StatusCodes.Status404NotFound);
    }
}
