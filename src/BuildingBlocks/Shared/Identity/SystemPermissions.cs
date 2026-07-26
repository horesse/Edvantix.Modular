namespace EDV.Framework.Shared.Identity;

/// <summary>
/// Сквозные разрешения платформы, не принадлежащие конкретному бизнес-модулю.
/// Регистрируются автоматически при вызове <c>AddPlatform</c>.
/// </summary>
public static class SystemPermissions
{
    public static class Hangfire
    {
        public const string Resource = nameof(Hangfire);
        public const string View = $"Permissions.{Resource}.View";
    }

    public static class Dashboard
    {
        public const string Resource = nameof(Dashboard);
        public const string View = $"Permissions.{Resource}.View";
    }

    /// <summary>
    /// Разрешения на уровне платформы, доступные только SuperAdmin (роль Admin корневого арендатора).
    /// Помечены <c>IsRoot=true</c>, что означает, что <see cref="PermissionConstants.Admin"/> 
    /// исключает их для некорневых арендаторов, а <see cref="PermissionConstants.Root"/> включает 
    /// их для корневого арендатора. Используйте для межарендаторских операций: управление арендаторами,
    /// тарифами, общесистемными аудитами и механизмом имперсонализации платформы.
    /// </summary>
    public static class Platform
    {
        public const string Tenants = $"{nameof(Platform)}.Tenants";
        public const string Plans = $"{nameof(Platform)}.Plans";
        public const string Subscriptions = $"{nameof(Platform)}.Subscriptions";
        public const string Invoices = $"{nameof(Platform)}.Invoices";
        public const string Webhooks = $"{nameof(Platform)}.Webhooks";
        public const string Audits = $"{nameof(Platform)}.Audits";
        public const string Users = $"{nameof(Platform)}.Users";
    }

    public static IReadOnlyList<AppPermission> All { get; } =
    [
        new("Просмотр Hangfire", ActionConstants.View, Hangfire.Resource, IsBasic: true),
        new("Просмотр панели управления", ActionConstants.View, Dashboard.Resource, IsBasic: true),

        // Платформа · межарендаторская — только для SuperAdmin.
        new("Просмотр всех арендаторов", ActionConstants.View, Platform.Tenants, IsRoot: true),
        new("Создание арендаторов", ActionConstants.Create, Platform.Tenants, IsRoot: true),
        new("Обновление арендаторов", ActionConstants.Update, Platform.Tenants, IsRoot: true),
        new("Приостановка арендаторов", "Suspend", Platform.Tenants, IsRoot: true),
        new("Удаление арендаторов", ActionConstants.Delete, Platform.Tenants, IsRoot: true),

        new("Управление тарифами", "Manage", Platform.Plans, IsRoot: true),
        new("Управление подписками", "Manage", Platform.Subscriptions, IsRoot: true),
        new("Администрирование всех счетов", "Admin", Platform.Invoices, IsRoot: true),
        new("Администрирование всех вебхуков", "Admin", Platform.Webhooks, IsRoot: true),

        new("Просмотр межарендаторских аудитов", "ViewAll", Platform.Audits, IsRoot: true),
        new("Имперсонализация между арендаторами", "Impersonate", Platform.Users, IsRoot: true),
    ];
}