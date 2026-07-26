using EDV.Modules.Billing.Contracts;
using EDV.Modules.Billing.Data;
using EDV.Modules.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Billing.IntegrationEventHandlers;

/// <summary>
/// Общая логика учёта подписок для обработчиков интеграционных событий жизненного цикла тенанта:
/// у тенанта может быть не более одной активной подписки, поэтому запуск новой отменяет текущую.
/// </summary>
internal static class TenantSubscriptionMaintenance
{
    public static async Task ReplaceActiveSubscriptionAsync(
        BillingDbContext db,
        string tenantId,
        Guid planId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        var active = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Status == SubscriptionStatus.Active, cancellationToken)
            .ConfigureAwait(false);
        active?.Cancel(startUtc);

        db.Subscriptions.Add(Subscription.Create(tenantId, planId, startUtc, endUtc));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Продление того же тарифа: продлевает окончание активной подписки, чтобы <c>Subscription.EndUtc</c>
    /// оставался синхронизирован с обновлённым <c>ValidUpto</c> тенанта (иначе срок подписки на дашборде
    /// отстанет от фактически применяемого срока действия). Идемпотентно благодаря <see cref="Subscription.Extend"/>.
    /// </summary>
    public static async Task ExtendActiveSubscriptionAsync(
        BillingDbContext db,
        string tenantId,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        var active = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Status == SubscriptionStatus.Active, cancellationToken)
            .ConfigureAwait(false);
        if (active is null)
        {
            return;
        }

        active.Extend(endUtc);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
