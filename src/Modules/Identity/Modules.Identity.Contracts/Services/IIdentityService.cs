using System.Security.Claims;

namespace EDV.Modules.Identity.Contracts.Services;

public interface IIdentityService
{
    /// <summary>
    /// Проверяет предоставленные учётные данные пользователя и возвращает уникальный идентификатор субъекта с соответствующими claims.
    /// </summary>
    /// <param name="email">Email или имя пользователя</param>
    /// <param name="password">Пароль пользователя</param>
    /// <param name="twoFactorCode">Необязательный код двухфакторной аутентификации</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Идентификатор субъекта и claims, или null, если недействительно</returns>
    Task<(string Subject, IEnumerable<Claim> Claims)?>
        ValidateCredentialsAsync(string email, string password, string? twoFactorCode = null, CancellationToken ct = default);

    /// <summary>
    /// Проверяет refresh-токен и возвращает его claims, если он действителен.
    /// </summary>
    Task<(string Subject, IEnumerable<Claim> Claims)?>
        ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Сохраняет хешированный refresh-токен для указанного субъекта.
    /// </summary>
    Task StoreRefreshTokenAsync(string subject, string refreshToken, DateTime expiresAtUtc, CancellationToken ct = default);

    /// <summary>
    /// Формирует набор claims для пользователя в произвольном арендаторе, обходя фильтры запросов по арендатору Finbuckle.
    /// Используется в потоках имперсонализации и завершения имперсонализации, когда текущий контекст арендатора
    /// запроса отличается от арендатора целевого пользователя. Возвращает null, если пользователь не найден.
    /// </summary>
    Task<(string Subject, IEnumerable<Claim> Claims)?>
        BuildClaimsForUserAsync(string userId, string tenantId, CancellationToken ct = default);
}