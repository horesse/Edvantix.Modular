using EDV.Framework.Core.Domain;

namespace EDV.Modules.Identity.Domain.Events;

/// <summary>Возникает при отзыве сессии пользователя.</summary>
public sealed record SessionRevokedEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    string UserId,
    Guid SessionId,
    string? RevokedBy,
    string? Reason,
    string? CorrelationId = null,
    string? TenantId = null
) : DomainEvent(EventId, OccurredOnUtc, CorrelationId, TenantId)
{
    public static SessionRevokedEvent Create(string userId, Guid sessionId, string? revokedBy = null, string? reason = null, string? correlationId = null, string? tenantId = null)
        => new(Guid.NewGuid(), DateTimeOffset.UtcNow, userId, sessionId, revokedBy, reason, correlationId, tenantId);
}