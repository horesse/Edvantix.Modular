using EDV.Framework.Core.Domain;

namespace EDV.Modules.Identity.Domain.Events;

/// <summary>Возникает при активации аккаунта пользователя.</summary>
public sealed record UserActivatedEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    string UserId,
    string? ActivatedBy,
    string? CorrelationId = null,
    string? TenantId = null
) : DomainEvent(EventId, OccurredOnUtc, CorrelationId, TenantId)
{
    public static UserActivatedEvent Create(string userId, string? activatedBy = null, string? correlationId = null, string? tenantId = null)
        => new(Guid.NewGuid(), DateTimeOffset.UtcNow, userId, activatedBy, correlationId, tenantId);
}