using EDV.Framework.Shared.Identity;

namespace EDV.Modules.Identity.Contracts.Authorization;

/// <summary>
/// Разрешения модуля Identity. Единый источник истины — строковые литералы (используются
/// в <c>.RequirePermission(...)</c>) и список <see cref="All"/> формируются из одних
/// и тех же констант Resource/Action ниже, поэтому они не могут расходиться.
/// </summary>
public static class IdentityPermissions
{
    public static class Users
    {
        public const string Resource = nameof(Users);
        public const string View = $"Permissions.{Resource}.View";
        public const string Search = $"Permissions.{Resource}.Search";
        public const string Create = $"Permissions.{Resource}.Create";
        public const string Update = $"Permissions.{Resource}.Update";
        public const string Delete = $"Permissions.{Resource}.Delete";
        public const string Export = $"Permissions.{Resource}.Export";
        public const string ManageRoles = $"Permissions.{Resource}.ManageRoles";
        public const string Impersonate = $"Permissions.{Resource}.Impersonate";
        public const string ConfirmEmail = $"Permissions.{Resource}.ConfirmEmail";
    }

    public static class UserRoles
    {
        public const string Resource = nameof(UserRoles);
        public const string View = $"Permissions.{Resource}.View";
        public const string Update = $"Permissions.{Resource}.Update";
    }

    public static class Roles
    {
        public const string Resource = nameof(Roles);
        public const string View = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
        public const string Update = $"Permissions.{Resource}.Update";
        public const string Delete = $"Permissions.{Resource}.Delete";
    }

    public static class RoleClaims
    {
        public const string Resource = nameof(RoleClaims);
        public const string View = $"Permissions.{Resource}.View";
        public const string Update = $"Permissions.{Resource}.Update";
    }

    public static class Sessions
    {
        public const string Resource = nameof(Sessions);
        public const string View = $"Permissions.{Resource}.View";
        public const string Revoke = $"Permissions.{Resource}.Revoke";
        public const string ViewAll = $"Permissions.{Resource}.ViewAll";
        public const string RevokeAll = $"Permissions.{Resource}.RevokeAll";
    }

    public static class Groups
    {
        public const string Resource = nameof(Groups);
        public const string View = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
        public const string Update = $"Permissions.{Resource}.Update";
        public const string Delete = $"Permissions.{Resource}.Delete";
        public const string ManageMembers = $"Permissions.{Resource}.ManageMembers";
    }

    public static class Impersonation
    {
        public const string Resource = nameof(Impersonation);

        /// <summary>Просмотр разрешений на имперсонализацию (доступ только для чтения к истории разрешений).</summary>
        public const string View = $"Permissions.{Resource}.View";

        /// <summary>Отозвать активное разрешение на имперсонализацию до его естественного истечения.</summary>
        public const string Revoke = $"Permissions.{Resource}.Revoke";
    }

    public static IReadOnlyList<AppPermission> All { get; } =
    [
        new("Просмотр пользователей", ActionConstants.View, Users.Resource, IsBasic: true),
        new("Поиск пользователей", ActionConstants.Search, Users.Resource),
        new("Создание пользователей", ActionConstants.Create, Users.Resource),
        new("Обновление пользователей", ActionConstants.Update, Users.Resource),
        new("Удаление пользователей", ActionConstants.Delete, Users.Resource),
        new("Экспорт пользователей", ActionConstants.Export, Users.Resource),
        new("Управление ролями пользователей", "ManageRoles", Users.Resource),
        new("Имперсонализация пользователя", "Impersonate", Users.Resource),
        new("Подтверждение email пользователя", "ConfirmEmail", Users.Resource),

        new("Просмотр ролей пользователя", ActionConstants.View, UserRoles.Resource, IsBasic: true),
        new("Обновление ролей пользователя", ActionConstants.Update, UserRoles.Resource),

        new("Просмотр ролей", ActionConstants.View, Roles.Resource, IsBasic: true),
        new("Создание ролей", ActionConstants.Create, Roles.Resource),
        new("Обновление ролей", ActionConstants.Update, Roles.Resource),
        new("Удаление ролей", ActionConstants.Delete, Roles.Resource),

        new("Просмотр прав ролей", ActionConstants.View, RoleClaims.Resource, IsBasic: true),
        new("Обновление прав ролей", ActionConstants.Update, RoleClaims.Resource),

        new("Просмотр моих сессий", ActionConstants.View, Sessions.Resource, IsBasic: true),
        new("Отзыв моих сессий", "Revoke", Sessions.Resource, IsBasic: true),
        new("Просмотр всех сессий", "ViewAll", Sessions.Resource),
        new("Отзыв любой сессии", "RevokeAll", Sessions.Resource),

        new("Просмотр групп", ActionConstants.View, Groups.Resource, IsBasic: true),
        new("Создание групп", ActionConstants.Create, Groups.Resource),
        new("Обновление групп", ActionConstants.Update, Groups.Resource),
        new("Удаление групп", ActionConstants.Delete, Groups.Resource),
        new("Управление участниками групп", "ManageMembers", Groups.Resource),

        new("Просмотр разрешений на имперсонализацию", ActionConstants.View, Impersonation.Resource),
        new("Отзыв разрешений на имперсонализацию", "Revoke", Impersonation.Resource),
    ];
}