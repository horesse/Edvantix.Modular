using EDV.Framework.Eventing.Abstractions;

namespace EDV.Modules.Identity.Contracts.Events;

/// <summary>
/// Интеграционное событие, возникающее при генерации JWT-токена для пользователя.
/// Предназначено в первую очередь как пример события для тестирования конвейера событий/outbox.
/// </summary>
public sealed record TokenGeneratedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    string UserId,
    string Email,
    string ClientId,
    string IpAddress,
    string UserAgent,
    string TokenFingerprint,
    DateTime AccessTokenExpiresAtUtc)
    : IIntegrationEvent;