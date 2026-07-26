using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Data;
using EDV.Modules.Billing.Services;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Billing.Features.v1.Invoices.GetInvoicePdf;

public sealed class GetInvoicePdfQueryHandler(
    BillingDbContext dbContext,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
    IInvoicePdfRenderer renderer)
    : IQueryHandler<GetInvoicePdfQuery, InvoicePdfResult>
{
    public async ValueTask<InvoicePdfResult> Handle(GetInvoicePdfQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // BillingDbContext не фильтруется по тенанту: root может скачать PDF счёта ЛЮБОГО тенанта;
        // вызывающий в контексте тенанта ограничен своим, поэтому идентификатор из другого тенанта
        // приводит к 404 и никогда не приводит к утечке PDF.
        var callerTenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Требуется контекст тенанта.");
        var isRoot = callerTenantId == MultitenancyConstants.Root.Id;

        var invoice = await dbContext.Invoices.AsNoTracking()
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(
                i => i.Id == query.InvoiceId && (isRoot || i.TenantId == callerTenantId),
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Счёт {query.InvoiceId} не найден.");

        var dto = invoice.ToDto();
        var content = renderer.Render(dto);
        return new InvoicePdfResult(content, $"{dto.InvoiceNumber}.pdf");
    }
}
