using EDV.Framework.Shared.Identity;

namespace EDV.Modules.Multitenancy.Contracts.Authorization;

public static class MultitenancyPermissions
{
    public static class Tenants
    {
        public const string Resource = nameof(Tenants);
        public const string View                = $"Permissions.{Resource}.View";
        public const string Create              = $"Permissions.{Resource}.Create";
        public const string Update              = $"Permissions.{Resource}.Update";
        public const string UpgradeSubscription = $"Permissions.{Resource}.UpgradeSubscription";
        public const string ViewTheme           = $"Permissions.{Resource}.ViewTheme";
        public const string UpdateTheme         = $"Permissions.{Resource}.UpdateTheme";
    }

    public static IReadOnlyList<AppPermission> All { get; } =
    [
        new("Просмотр тенантов",                  ActionConstants.View,                Tenants.Resource, IsRoot: true),
        new("Создание тенантов",                  ActionConstants.Create,              Tenants.Resource, IsRoot: true),
        new("Изменение тенантов",                 ActionConstants.Update,              Tenants.Resource, IsRoot: true),
        new("Повышение тарифа подписки тенанта",  ActionConstants.UpgradeSubscription, Tenants.Resource, IsRoot: true),
        new("Просмотр темы тенанта",              "ViewTheme",                         Tenants.Resource, IsBasic: true),
        new("Изменение темы тенанта",              "UpdateTheme",                       Tenants.Resource),
    ];
}
