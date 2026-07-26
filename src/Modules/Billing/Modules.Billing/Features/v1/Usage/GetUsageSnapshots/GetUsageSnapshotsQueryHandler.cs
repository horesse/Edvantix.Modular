using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Contracts.Dtos;
using EDV.Modules.Billing.Contracts.v1.Usage;
using EDV.Modules.Billing.Data;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Billing.Features.v1.Usage.GetUsageSnapshots;

public sealed class GetUsageSnapshotsQueryHandler(
    BillingDbContext dbContext,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IQueryHandler<GetUsageSnapshotsQuery, IReadOnlyList<UsageSnapshotDto>>
{
    public async ValueTask<IReadOnlyList<UsageSnapshotDto>> Handle(GetUsageSnapshotsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // UsageSnapshots не фильтруется по тенанту. Только root-оператор может читать данные по всем
        // тенантам (опционально сужаемые через query.TenantId); любой другой вызывающий ограничен своим тенантом.
        var callerTenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Требуется контекст тенанта.");
        var isRoot = callerTenantId == MultitenancyConstants.Root.Id;
        var tenantFilter = isRoot ? query.TenantId : callerTenantId;

        var q = dbContext.UsageSnapshots.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(tenantFilter))
        {
            q = q.Where(s => s.TenantId == tenantFilter);
        }
        if (query.PeriodYear is not null)
        {
            q = q.Where(s => s.PeriodYear == query.PeriodYear);
        }
        if (query.PeriodMonth is not null)
        {
            q = q.Where(s => s.PeriodMonth == query.PeriodMonth);
        }

        var snaps = await q
            .OrderByDescending(s => s.PeriodYear).ThenByDescending(s => s.PeriodMonth)
            .ThenBy(s => s.TenantId).ThenBy(s => s.Resource)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return snaps
            .Select(s => new UsageSnapshotDto(s.Id, s.TenantId, s.PeriodYear, s.PeriodMonth, s.Resource, s.UsedUnits, s.LimitUnits, s.Overage, s.CapturedAtUtc))
            .ToList();
    }
}
