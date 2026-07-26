using EDV.Framework.Eventing.Abstractions;

namespace EDV.Modules.Multitenancy.Contracts.Events;

/// <summary>
/// Возникает при ежедневном сканировании истечения сроков, когда активный тенант находится в пределах
/// настроенного времени предупреждения до своего <c>ValidUpto</c> (но срок ещё не истёк). Потребители
/// уведомляют тенанта, чтобы он мог вовремя продлить подписку.
/// </summary>
public sealed record TenantNearingExpiryIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    string TenantName,
    string AdminEmail,
    string? PlanKey,
    DateTime ValidUpto,
    DateTime GraceEndsUtc,
    int DaysRemaining)
    : IIntegrationEvent;
