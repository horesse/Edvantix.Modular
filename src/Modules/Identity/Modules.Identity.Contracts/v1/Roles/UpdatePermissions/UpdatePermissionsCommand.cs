using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Roles.UpdatePermissions;

public class UpdatePermissionsCommand : ICommand<string>
{
    /// <summary>
    /// Идентификатор роли для обновления.
    /// </summary>
    public string RoleId { get; init; } = default!;

    /// <summary>
    /// Список разрешений для назначения роли.
    /// </summary>
    public List<string> Permissions { get; init; } = [];
}