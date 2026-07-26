using EDV.Framework.Shared.Identity;

namespace EDV.Modules.Billing.Contracts.Authorization;

public static class BillingPermissions
{
    public const string Resource = "Billing";
    public const string View = $"Permissions.{Resource}.View";
    public const string Manage = $"Permissions.{Resource}.Manage";

    public static IReadOnlyList<AppPermission> All { get; } =
    [
        new("Просмотр биллинга", ActionConstants.View, Resource, IsBasic: true),
        new("Управление биллингом", "Manage", Resource),
    ];
}