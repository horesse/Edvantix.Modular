using EDV.Framework.Eventing.Abstractions;
using EDV.Modules.Billing.Data;
using EDV.Modules.Billing.Services;
using EDV.Modules.Multitenancy.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Billing.IntegrationEventHandlers;

/// <summary>
/// Реагирует на создание тенанта и подписку на тариф: запускает активную подписку и выставляет счёт
/// по подписке за срок. Создание счёта идемпотентно (защищено номером счёта).
/// </summary>
public sealed class TenantSubscribedIntegrationEventHandler(
    BillingDbContext db,
    IBillingService billing,
    ILogger<TenantSubscribedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TenantSubscribedIntegrationEvent>
{
    public async Task HandleAsync(TenantSubscribedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var tenantId = @event.TenantId
            ?? throw new InvalidOperationException("В TenantSubscribedIntegrationEvent отсутствует TenantId.");

        await TenantSubscriptionMaintenance.ReplaceActiveSubscriptionAsync(
            db, tenantId, @event.PlanId, @event.PeriodStartUtc, @event.PeriodEndUtc, ct).ConfigureAwait(false);

        await billing.CreateSubscriptionInvoiceAsync(
            tenantId, @event.PlanId, @event.PeriodStartUtc, @event.PeriodEndUtc, ct).ConfigureAwait(false);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "[Billing] тенант {TenantId} подписан на тариф {PlanKey}; срок истекает {End:o}",
                tenantId, @event.PlanKey, @event.PeriodEndUtc);
        }
    }
}
