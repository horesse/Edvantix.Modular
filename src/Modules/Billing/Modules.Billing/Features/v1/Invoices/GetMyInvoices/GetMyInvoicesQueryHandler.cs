using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Shared.Persistence;
using EDV.Modules.Billing.Contracts.Dtos;
using EDV.Modules.Billing.Contracts.v1.Invoices;
using EDV.Modules.Billing.Data;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Billing.Features.v1.Invoices.GetMyInvoices;

public sealed class GetMyInvoicesQueryHandler(
    BillingDbContext dbContext,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IQueryHandler<GetMyInvoicesQuery, PagedResponse<InvoiceDto>>
{
    public async ValueTask<PagedResponse<InvoiceDto>> Handle(GetMyInvoicesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Требуется контекст тенанта.");

        var q = dbContext.Invoices.AsNoTracking()
            .Include(i => i.LineItems)
            .Where(i => i.TenantId == tenantId);
        if (query.Status is not null)
        {
            q = q.Where(i => i.Status == query.Status);
        }
        if (query.PeriodYear is not null)
        {
            q = q.Where(i => i.PeriodYear == query.PeriodYear);
        }
        if (query.PeriodMonth is not null)
        {
            q = q.Where(i => i.PeriodMonth == query.PeriodMonth);
        }

        var total = await q.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var invoices = await q
            .OrderByDescending(i => i.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResponse<InvoiceDto>
        {
            Items = invoices.Select(i => i.ToDto()).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.PageSize)
        };
    }
}
