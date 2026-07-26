using System.Collections.ObjectModel;

namespace EDV.Framework.Shared.Identity;

public static class RoleConstants
{
    public const string Admin = nameof(Admin);
    public const string Basic = nameof(Basic);

    /// <summary>
    /// Базовые роли, предоставляемые платформой.
    /// </summary>
    public static IReadOnlyList<string> DefaultRoles { get; } = new ReadOnlyCollection<string>(new[]
    {
        Admin,
        Basic
    });

    /// <summary>
    /// Определяет, является ли роль стандартной ролью, определённой платформой.
    /// </summary>
    public static bool IsDefault(string roleName) => DefaultRoles.Contains(roleName);
}