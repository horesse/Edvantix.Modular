using EDV.Framework.Eventing.Abstractions;

namespace EDV.Modules.Multitenancy.Contracts.Events;

/// <summary>
/// Возникает при создании тенанта и оформлении подписки на тарифный план. Модуль Billing реагирует
/// созданием активной подписки и выставлением счёта за подписку на этот срок.
/// </summary>
public sealed record TenantSubscribedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid PlanId,
    string PlanKey,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc)
    : IIntegrationEvent;
