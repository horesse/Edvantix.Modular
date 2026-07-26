using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Users.AdminConfirmEmail;

/// <summary>
/// Административно подтверждает email пользователя (без токена подтверждения). Ограничено
/// разрешением <c>Permissions.Users.ConfirmEmail</c> в конечной точке.
/// </summary>
public sealed record AdminConfirmEmailCommand(string UserId) : ICommand<Unit>;