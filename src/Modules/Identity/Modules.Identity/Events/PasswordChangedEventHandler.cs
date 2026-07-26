using EDV.Modules.Identity.Domain.Events;
using Mediator;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Identity.Events;

/// <summary>
/// Обрабатывает доменное событие PasswordChangedEvent.
/// </summary>
public sealed class PasswordChangedHandler(
    ILogger<PasswordChangedHandler> logger)
    : INotificationHandler<PasswordChangedEvent>
{
    public ValueTask Handle(PasswordChangedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Пароль изменён для пользователя {UserId} (сброс: {WasReset})",
                notification.UserId,
                notification.WasReset);
        }

        return ValueTask.CompletedTask;
    }
}
