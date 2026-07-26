using EDV.Modules.Identity.Domain.Events;
using Mediator;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Identity.Events;

/// <summary>
/// Обрабатывает доменное событие UserRoleAssignedEvent.
/// </summary>
public sealed class UserRoleAssignedHandler(
    ILogger<UserRoleAssignedHandler> logger)
    : INotificationHandler<UserRoleAssignedEvent>
{
    public ValueTask Handle(UserRoleAssignedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Роли назначены пользователю {UserId}: {Roles}",
                notification.UserId,
                string.Join(", ", notification.AssignedRoles));
        }

        return ValueTask.CompletedTask;
    }
}
