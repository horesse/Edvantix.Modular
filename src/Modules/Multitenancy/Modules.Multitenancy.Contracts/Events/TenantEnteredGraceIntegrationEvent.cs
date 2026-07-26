using EDV.Framework.Eventing.Abstractions;

namespace EDV.Modules.Multitenancy.Contracts.Events;

/// <summary>
/// Возникает при ежедневном сканировании истечения сроков, когда тенант прошёл отметку <c>ValidUpto</c>,
/// но всё ещё находится в льготном периоде (доступ сохраняется). Потребители предупреждают тенанта об
/// обратном отсчёте льготного периода.
/// </summary>
public sealed record TenantEnteredGraceIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    string TenantName,
    string AdminEmail,
    string? PlanKey,
    DateTime ValidUpto,
    DateTime GraceEndsUtc)
    : IIntegrationEvent;
