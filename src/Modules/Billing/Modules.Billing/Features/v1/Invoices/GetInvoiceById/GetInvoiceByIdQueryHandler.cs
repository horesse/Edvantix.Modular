using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Contracts.Dtos;
using EDV.Modules.Billing.Contracts.v1.Invoices;
using EDV.Modules.Billing.Data;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Billing.Features.v1.Invoices.GetInvoiceById;

public sealed class GetInvoiceByIdQueryHandler(
    BillingDbContext dbContext,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IQueryHandler<GetInvoiceByIdQuery, InvoiceDto>
{
    public async ValueTask<InvoiceDto> Handle(GetInvoiceByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // BillingDbContext не фильтруется по тенанту (сырой DbContext для видимости между тенантами
        // администратором): root читает любой счёт по идентификатору; вызывающий в контексте тенанта
        // ограничен своим тенантом и не может прочитать чужой счёт. Аналогично GetSubscriptionQueryHandler.
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

        return invoice.ToDto();
    }
}
