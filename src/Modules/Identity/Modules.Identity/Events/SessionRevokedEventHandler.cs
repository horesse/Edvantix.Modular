using EDV.Modules.Identity.Domain.Events;
using Mediator;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Identity.Events;

/// <summary>
/// Обрабатывает доменное событие SessionRevokedEvent.
/// </summary>
public sealed class SessionRevokedHandler(
    ILogger<SessionRevokedHandler> logger)
    : INotificationHandler<SessionRevokedEvent>
{
    public ValueTask Handle(SessionRevokedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Сессия {SessionId} отозвана для пользователя {UserId} пользователем {RevokedBy}: {Reason}",
                notification.SessionId,
                notification.UserId,
                notification.RevokedBy,
                notification.Reason);
        }

        return ValueTask.CompletedTask;
    }
}
