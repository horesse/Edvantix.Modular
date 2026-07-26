using EDV.Modules.Identity.Domain.Events;
using Mediator;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Identity.Events;

/// <summary>
/// Обрабатывает доменное событие UserActivatedEvent.
/// </summary>
public sealed class UserActivatedHandler(
    ILogger<UserActivatedHandler> logger)
    : INotificationHandler<UserActivatedEvent>
{
    public ValueTask Handle(UserActivatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Пользователь {UserId} активирован пользователем {ActivatedBy}",
                notification.UserId,
                notification.ActivatedBy);
        }

        return ValueTask.CompletedTask;
    }
}
