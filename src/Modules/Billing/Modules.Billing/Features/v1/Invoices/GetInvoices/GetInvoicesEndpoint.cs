using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Billing.Contracts;
using EDV.Modules.Billing.Contracts.Authorization;
using EDV.Modules.Billing.Contracts.v1.Invoices;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Billing.Features.v1.Invoices.GetInvoices;

public static class GetInvoicesEndpoint
{
    internal static RouteHandlerBuilder MapGetInvoicesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/invoices",
                (string? tenantId, InvoiceStatus? status, int? periodYear, int? periodMonth,
                 int pageNumber, int pageSize, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetInvoicesQuery(
                        tenantId,
                        status,
                        periodYear,
                        periodMonth,
                        pageNumber <= 0 ? 1 : pageNumber,
                        pageSize <= 0 ? 20 : Math.Min(pageSize, 100)), ct))
            .WithName("GetInvoices")
            .WithSummary("Список счетов по всем тенантам (администратор)")
            .RequirePermission(BillingPermissions.View);
    }
}
