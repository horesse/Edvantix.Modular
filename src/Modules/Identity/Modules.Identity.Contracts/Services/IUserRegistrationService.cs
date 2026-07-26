using System.Security.Claims;

namespace EDV.Modules.Identity.Contracts.Services;

/// <summary>
/// Сервис для регистрации пользователей и внешней аутентификации.
/// </summary>
public interface IUserRegistrationService
{
    /// <summary>
    /// Регистрирует нового пользователя с паролем.
    /// </summary>
    Task<string> RegisterAsync(
        string firstName,
        string lastName,
        string email,
        string userName,
        string password,
        string confirmPassword,
        string phoneNumber,
        string origin,
        CancellationToken cancellationToken);

    /// <summary>
    /// Получает или создаёт пользователя из principal внешней аутентификации.
    /// </summary>
    Task<string> GetOrCreateFromPrincipalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Подтверждает email-адрес пользователя.
    /// </summary>
    Task<string> ConfirmEmailAsync(string userId, string code, string tenant, CancellationToken cancellationToken);

    /// <summary>
    /// Административно помечает email пользователя как подтверждённый без токена подтверждения.
    /// Ограничено разрешением <c>Permissions.Users.ConfirmEmail</c> в конечной точке. Идемпотентно.
    /// </summary>
    Task AdminConfirmEmailAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Повторно отправляет ссылку для подтверждения email пользователю, который ещё не подтвердил свой адрес.
    /// <paramref name="origin"/> — базовый URL запроса, используемый для построения ссылки подтверждения.
    /// Выбрасывает исключение, если email пользователя уже подтверждён.
    /// </summary>
    Task ResendConfirmationEmailAsync(string userId, string origin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Подтверждает номер телефона пользователя.
    /// </summary>
    Task<string> ConfirmPhoneNumberAsync(string userId, string code, CancellationToken cancellationToken = default);
}