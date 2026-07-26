namespace EDV.Modules.Identity.Contracts.Services;

/// <summary>
/// Сервис для операций с разрешениями пользователей.
/// </summary>
public interface IUserPermissionService
{
    /// <summary>
    /// Получает все разрешения пользователя.
    /// </summary>
    Task<List<string>?> GetPermissionsAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Проверяет, есть ли у пользователя конкретное разрешение.
    /// </summary>
    Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Инвалидирует кэш разрешений пользователя.
    /// </summary>
    Task InvalidatePermissionCacheAsync(string userId, CancellationToken cancellationToken);
}