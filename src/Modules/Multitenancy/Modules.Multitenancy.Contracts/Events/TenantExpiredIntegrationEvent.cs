using EDV.Framework.Eventing.Abstractions;

namespace EDV.Modules.Multitenancy.Contracts.Events;

/// <summary>
/// Возникает при ежедневном сканировании истечения сроков, когда тенант прошёл отметку
/// <c>ValidUpto + льготный период</c> и теперь жёстко заблокирован. Потребители уведомляют тенанта
/// о приостановке доступа до продления.
/// </summary>
public sealed record TenantExpiredIntegrationEvent(
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
