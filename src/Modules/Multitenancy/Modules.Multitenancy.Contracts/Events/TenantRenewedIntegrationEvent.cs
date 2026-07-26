using EDV.Framework.Eventing.Abstractions;

namespace EDV.Modules.Multitenancy.Contracts.Events;

/// <summary>
/// Возникает при продлении тенанта (и, возможно, переключении на другой план). Модуль Billing реагирует
/// заменой подписки при изменении плана и выставлением счёта за новый срок.
/// </summary>
public sealed record TenantRenewedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid PlanId,
    string PlanKey,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    bool PlanChanged)
    : IIntegrationEvent;
