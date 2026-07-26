using EDV.Framework.Eventing.Abstractions;
using EDV.Modules.Billing.Data;
using EDV.Modules.Billing.Services;
using EDV.Modules.Multitenancy.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Billing.IntegrationEventHandlers;

/// <summary>
/// Реагирует на продление тенанта: при изменении тарифа заменяет активную подписку на новый тариф;
/// в любом случае выставляет счёт по подписке за новый срок (идемпотентно, защищено номером счёта).
/// </summary>
public sealed class TenantRenewedIntegrationEventHandler(
    BillingDbContext db,
    IBillingService billing,
    ILogger<TenantRenewedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TenantRenewedIntegrationEvent>
{
    public async Task HandleAsync(TenantRenewedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var tenantId = @event.TenantId
            ?? throw new InvalidOperationException("В TenantRenewedIntegrationEvent отсутствует TenantId.");

        if (@event.PlanChanged)
        {
            await TenantSubscriptionMaintenance.ReplaceActiveSubscriptionAsync(
                db, tenantId, @event.PlanId, @event.PeriodStartUtc, @event.PeriodEndUtc, ct).ConfigureAwait(false);
        }
        else
        {
            // Продление того же тарифа: продлеваем срок активной подписки, чтобы EndUtc следовал за
            // обновлённым ValidUpto (иначе "текущий срок"/срок действия на дашборде отстанет от фактически применяемого).
            await TenantSubscriptionMaintenance.ExtendActiveSubscriptionAsync(
                db, tenantId, @event.PeriodEndUtc, ct).ConfigureAwait(false);
        }

        await billing.CreateSubscriptionInvoiceAsync(
            tenantId, @event.PlanId, @event.PeriodStartUtc, @event.PeriodEndUtc, ct).ConfigureAwait(false);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "[Billing] тенант {TenantId} продлён на тарифе {PlanKey} (planChanged={PlanChanged}); срок истекает {End:o}",
                tenantId, @event.PlanKey, @event.PlanChanged, @event.PeriodEndUtc);
        }
    }
}
