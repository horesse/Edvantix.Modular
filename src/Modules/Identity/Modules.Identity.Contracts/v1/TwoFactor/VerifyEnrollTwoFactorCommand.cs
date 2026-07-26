using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.TwoFactor;

/// <summary>
/// Проверяет 6-значный код TOTP из приложения-аутентификатора пользователя. При успехе
/// двухфакторная аутентификация включается для пользователя — последующие входы должны
/// включать действительный код.
/// </summary>
public sealed record VerifyEnrollTwoFactorCommand(string Code) : ICommand<bool>;