using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Contracts.Dtos;
using EDV.Modules.Billing.Contracts.v1.Subscriptions;
using EDV.Modules.Billing.Data;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Billing.Features.v1.Subscriptions.GetSubscription;

public sealed class GetSubscriptionQueryHandler(
    BillingDbContext dbContext,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IQueryHandler<GetSubscriptionQuery, SubscriptionDto?>
{
    public async ValueTask<SubscriptionDto?> Handle(GetSubscriptionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var callerTenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Требуется контекст тенанта.");

        // BillingDbContext не фильтруется по тенанту, поэтому вызывающий в контексте тенанта
        // ограничен СВОЕЙ подпиской, и только root может передать произвольный идентификатор тенанта
        // (иначе возможно чтение данных другого тенанта).
        var tenantId = callerTenantId == MultitenancyConstants.Root.Id
            ? query.TenantId ?? callerTenantId
            : callerTenantId;

        var sub = await (from s in dbContext.Subscriptions.AsNoTracking()
                         join p in dbContext.Plans.AsNoTracking() on s.PlanId equals p.Id
                         where s.TenantId == tenantId
                            && s.Status == Contracts.SubscriptionStatus.Active
                         select new SubscriptionDto(s.Id, s.TenantId, s.PlanId, p.Key, s.StartUtc, s.EndUtc, s.Status))
                        .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return sub;
    }
}
