using EDV.Framework.Eventing.Abstractions;

namespace EDV.Modules.Identity.Contracts.Events;

/// <summary>
/// Интеграционное событие, возникающее при регистрации нового пользователя.
/// </summary>
public sealed record UserRegisteredIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    string UserId,
    string Email,
    string FirstName,
    string LastName)
    : IIntegrationEvent;