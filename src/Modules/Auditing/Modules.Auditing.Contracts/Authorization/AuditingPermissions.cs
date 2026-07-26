using EDV.Framework.Shared.Identity;

namespace EDV.Modules.Auditing.Contracts.Authorization;

public static class AuditingPermissions
{
    public static class AuditTrails
    {
        public const string Resource = nameof(AuditTrails);
        public const string View = $"Permissions.{Resource}.View";
        /// <summary>
        /// Разрешает запрашивать аудит по всем арендаторам, передавая фильтр TenantId.
        /// Без этого разрешения вызывающие видят только аудит своего собственного арендатора.
        /// </summary>
        public const string ViewCrossTenant = $"Permissions.{Resource}.ViewCrossTenant";
    }

    public static IReadOnlyList<AppPermission> All { get; } =
    [
        new("View Audit Trails", ActionConstants.View, AuditTrails.Resource, IsBasic: true),
        new("View Audit Trails Across Tenants", "ViewCrossTenant", AuditTrails.Resource, IsRoot: true),
    ];
}
