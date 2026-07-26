using System.Security.Claims;

namespace EDV.Framework.Core.Context;

/// <summary>
/// Представляет контекст текущего аутентифицированного пользователя с доступом к информации о пользователе и его утверждениям (claims).
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Возвращает отображаемое имя текущего пользователя.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Возвращает уникальный идентификатор текущего пользователя.
    /// </summary>
    /// <returns>Уникальный идентификатор пользователя.</returns>
    Guid GetUserId();

    /// <summary>
    /// Возвращает адрес электронной почты текущего пользователя.
    /// </summary>
    /// <returns>Адрес электронной почты пользователя или null, если недоступен.</returns>
    string? GetUserEmail();

    /// <summary>
    /// Возвращает идентификатор арендатора (tenant), к которому принадлежит текущий пользователь.
    /// </summary>
    /// <returns>Идентификатор арендатора или null, если контекст не является мультитенантным.</returns>
    string? GetTenant();

    /// <summary>
    /// Определяет, аутентифицирован ли текущий пользователь.
    /// </summary>
    /// <returns>true, если пользователь аутентифицирован; иначе false.</returns>
    bool IsAuthenticated();

    /// <summary>
    /// Определяет, состоит ли текущий пользователь в указанной роли.
    /// </summary>
    /// <param name="role">Роль для проверки.</param>
    /// <returns>true, если пользователь состоит в указанной роли; иначе false.</returns>
    bool IsInRole(string role);

    /// <summary>
    /// Возвращает все утверждения (claims), связанные с текущим пользователем.
    /// </summary>
    /// <returns>Коллекция утверждений пользователя или null, если утверждения отсутствуют.</returns>
    IEnumerable<Claim>? GetUserClaims();
}