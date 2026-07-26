using EDV.Modules.Identity.Domain.Events;
using Mediator;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Identity.Events;

/// <summary>
/// Обрабатывает доменное событие UserDeactivatedEvent.
/// </summary>
public sealed class UserDeactivatedHandler(
    ILogger<UserDeactivatedHandler> logger)
    : INotificationHandler<UserDeactivatedEvent>
{
    public ValueTask Handle(UserDeactivatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Пользователь {UserId} деактивирован пользователем {DeactivatedBy}: {Reason}",
                notification.UserId,
                notification.DeactivatedBy,
                notification.Reason);
        }

        return ValueTask.CompletedTask;
    }
}
