using EDV.Modules.Identity.Contracts.DTOs;
using System.Security.Claims;

namespace EDV.Modules.Identity.Contracts.Services;

public interface ITokenService
{
    /// <summary>
    /// Выпускает новый access и refresh токен для указанного субъекта.
    /// </summary>
    Task<TokenResponse> IssueAsync(
        string subject,
        IEnumerable<Claim> claims,
        string? tenant = null,
        CancellationToken ct = default);

    /// <summary>
    /// Выпускает кратковременный access токен без refresh токена. Используется в потоках
    /// (например, имперсонализация), где обновление намеренно запрещено. Передайте
    /// <paramref name="lifetime"/>, чтобы переопределить <c>JwtOptions.AccessTokenMinutes</c>
    /// по умолчанию (имперсонализация использует это, чтобы позволить оператору
    /// выбрать сессии на 10/15/30 минут).
    /// </summary>
    Task<(string AccessToken, DateTime ExpiresAtUtc)> IssueAccessOnlyAsync(
        string subject,
        IEnumerable<Claim> claims,
        TimeSpan? lifetime = null,
        CancellationToken ct = default);
}