namespace EDV.Modules.Identity.Contracts.Services;

/// <summary>
/// Сервис для операций с паролями пользователей.
/// </summary>
public interface IUserPasswordService
{
    /// <summary>
    /// Инициирует поток восстановления пароля, отправляя письмо для сброса.
    /// </summary>
    Task ForgotPasswordAsync(string email, string origin, CancellationToken cancellationToken);

    /// <summary>
    /// Сбрасывает пароль пользователя с использованием токена.
    /// </summary>
    Task ResetPasswordAsync(string email, string password, string token, CancellationToken cancellationToken);

    /// <summary>
    /// Изменяет пароль текущего пользователя.
    /// </summary>
    Task ChangePasswordAsync(string password, string newPassword, string confirmNewPassword, string userId, CancellationToken cancellationToken = default);
}