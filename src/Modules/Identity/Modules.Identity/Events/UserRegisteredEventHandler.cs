using EDV.Framework.Eventing.Abstractions;
using EDV.Modules.Identity.Contracts.Events;
using EDV.Modules.Identity.Domain.Events;
using Mediator;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Identity.Events;

/// <summary>
/// Обрабатывает доменное событие UserRegisteredEvent, публикуя интеграционное событие,
/// чтобы другие модули могли реагировать на регистрацию новых пользователей.
/// </summary>
public sealed class UserRegisteredHandler(
    IEventBus eventBus,
    ILogger<UserRegisteredHandler> logger)
    : INotificationHandler<UserRegisteredEvent>
{
    public async ValueTask Handle(UserRegisteredEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (logger.IsEnabled(LogLevel.Information))
        {
            // Минимизация PII: логируем только псевдонимный UserId, без адреса электронной почты.
            logger.LogInformation(
                "Пользователь зарегистрирован: {UserId}",
                notification.UserId);
        }

        var integrationEvent = new UserRegisteredIntegrationEvent(
            Id: notification.EventId,
            OccurredOnUtc: notification.OccurredOnUtc.UtcDateTime,
            TenantId: notification.TenantId,
            CorrelationId: notification.CorrelationId ?? notification.EventId.ToString(),
            Source: nameof(UserRegisteredHandler),
            UserId: notification.UserId,
            Email: notification.Email,
            FirstName: notification.FirstName ?? string.Empty,
            LastName: notification.LastName ?? string.Empty);

        await eventBus.PublishAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
    }
}
