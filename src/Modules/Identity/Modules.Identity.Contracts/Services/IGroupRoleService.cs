namespace EDV.Modules.Identity.Contracts.Services;

/// <summary>
/// Сервис для получения ролей, производных от членства в группах.
/// </summary>
public interface IGroupRoleService
{
    /// <summary>
    /// Получает все имена ролей, которые пользователь имеет через своё членство в группах.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя, для которого получаются групповые роли.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Список уникальных имён ролей из всех групп, в которых состоит пользователь.</returns>
    Task<IReadOnlyList<string>> GetUserGroupRolesAsync(string userId, CancellationToken ct = default);
}