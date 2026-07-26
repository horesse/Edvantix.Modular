using EDV.Framework.Core.Domain;

namespace EDV.Modules.Identity.Domain.Events;

/// <summary>Возникает при деактивации аккаунта пользователя.</summary>
public sealed record UserDeactivatedEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    string UserId,
    string? DeactivatedBy,
    string? Reason,
    string? CorrelationId = null,
    string? TenantId = null
) : DomainEvent(EventId, OccurredOnUtc, CorrelationId, TenantId)
{
    public static UserDeactivatedEvent Create(string userId, string? deactivatedBy = null, string? reason = null, string? correlationId = null, string? tenantId = null)
        => new(Guid.NewGuid(), DateTimeOffset.UtcNow, userId, deactivatedBy, reason, correlationId, tenantId);
}