using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Contracts.v1.Subscriptions;
using EDV.Modules.Billing.Data;
using EDV.Modules.Billing.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Billing.Features.v1.Subscriptions.AssignSubscription;

public sealed class AssignSubscriptionCommandHandler(
    BillingDbContext dbContext,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : ICommandHandler<AssignSubscriptionCommand, Guid>
{
    public async ValueTask<Guid> Handle(AssignSubscriptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Только root может указать произвольного тенанта; вызывающий в контексте тенанта ограничен
        // своим тенантом и не может (пере)назначить или отменить подписку другого тенанта, подставив
        // чужой идентификатор тенанта в тело запроса.
        var callerTenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Требуется контекст тенанта.");
        var isRoot = callerTenantId == MultitenancyConstants.Root.Id;
        var targetTenantId = isRoot ? command.TenantId : callerTenantId;

#pragma warning disable CA1308 // Plan keys are canonical lowercase slugs
        var key = command.PlanKey.ToLowerInvariant();
#pragma warning restore CA1308
        var plan = await dbContext.Plans.FirstOrDefaultAsync(p => p.Key == key && p.IsActive, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Активный тариф с ключом '{command.PlanKey}' не найден.");

        var now = DateTime.UtcNow;
        var current = await dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == targetTenantId && s.Status == Contracts.SubscriptionStatus.Active, cancellationToken)
            .ConfigureAwait(false);
        current?.Cancel(now);

        var subscription = Subscription.Create(targetTenantId, plan.Id, now);
        dbContext.Subscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return subscription.Id;
    }
}
