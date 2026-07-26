using EDV.Framework.Core.Domain;

namespace EDV.Modules.Identity.Domain.Events;

/// <summary>Возникает, когда пользователю назначаются роли.</summary>
public sealed record UserRoleAssignedEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    string UserId,
    IReadOnlyList<string> AssignedRoles,
    string? CorrelationId = null,
    string? TenantId = null
) : DomainEvent(EventId, OccurredOnUtc, CorrelationId, TenantId)
{
    public static UserRoleAssignedEvent Create(string userId, IEnumerable<string> assignedRoles, string? correlationId = null, string? tenantId = null)
        => new(Guid.NewGuid(), DateTimeOffset.UtcNow, userId, assignedRoles.ToList().AsReadOnly(), correlationId, tenantId);
}