using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Users.ResendConfirmationEmail;

/// <summary>
/// Повторно отправляет ссылку для подтверждения email неподтверждённому пользователю.
/// Ограничено разрешением <c>Permissions.Users.ConfirmEmail</c> в конечной точке.
/// <see cref="Origin"/> — базовый URL запроса, используемый для построения ссылки подтверждения
/// (устанавливается конечной точкой).
/// </summary>
public sealed record ResendConfirmationEmailCommand(string UserId, string Origin) : ICommand<Unit>;