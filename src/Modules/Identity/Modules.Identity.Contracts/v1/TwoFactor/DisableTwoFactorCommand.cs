using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.TwoFactor;

/// <summary>
/// Отключает двухфакторную аутентификацию для текущего пользователя. Требует текущий пароль
/// в качестве подтверждения, чтобы один только украденный access токен не мог понизить
/// безопасность учётной записи.
/// </summary>
public sealed record DisableTwoFactorCommand(string CurrentPassword) : ICommand<bool>;