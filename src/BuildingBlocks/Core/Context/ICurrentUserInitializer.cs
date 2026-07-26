using System.Security.Claims;

namespace EDV.Framework.Core.Context;

/// <summary>
/// Предоставляет методы для инициализации и установки контекста текущего пользователя.
/// </summary>
public interface ICurrentUserInitializer
{
    /// <summary>
    /// Устанавливает текущего пользователя на основе субъекта утверждений (claims principal).
    /// </summary>
    /// <param name="user">Субъект утверждений, представляющий аутентифицированного пользователя.</param>
    void SetCurrentUser(ClaimsPrincipal user);

    /// <summary>
    /// Устанавливает идентификатор текущего пользователя напрямую.
    /// </summary>
    /// <param name="userId">Уникальный идентификатор пользователя.</param>
    void SetCurrentUserId(string userId);
}