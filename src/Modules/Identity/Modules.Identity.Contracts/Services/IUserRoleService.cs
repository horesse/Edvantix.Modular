using EDV.Modules.Identity.Contracts.DTOs;

namespace EDV.Modules.Identity.Contracts.Services;

/// <summary>
/// Сервис для управления ролями пользователей.
/// </summary>
public interface IUserRoleService
{
    /// <summary>
    /// Назначает роли пользователю.
    /// </summary>
    Task<string> AssignRolesAsync(string userId, List<UserRoleDto> userRoles, CancellationToken cancellationToken);

    /// <summary>
    /// Получает все роли пользователя.
    /// </summary>
    Task<List<UserRoleDto>> GetUserRolesAsync(string userId, CancellationToken cancellationToken);
}