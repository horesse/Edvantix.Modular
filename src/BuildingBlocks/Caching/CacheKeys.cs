namespace EDV.Framework.Caching;

/// <summary>
/// Соглашения по ключам кэша и константы тегов, используемые в стартовом наборе EDV.
/// Ключи должны быть ограничены арендатором, где это применимо; теги позволяют выполнять массовую инвалидацию через
/// <see cref="Microsoft.Extensions.Caching.Hybrid.HybridCache.RemoveByTagAsync(string, System.Threading.CancellationToken)"/>.
/// </summary>
public static class CacheKeys
{
    /// <summary>Общеизвестные значения тегов для массовой инвалидации.</summary>
    public static class Tags
    {
        /// <summary>Тег, применяемый к каждой записи разрешений.</summary>
        public const string Permissions = "permissions";

        /// <summary>Тег, применяемый к каждой записи темы арендатора.</summary>
        public const string Themes = "themes";

        /// <summary>Тег, применяемый к каждой записи идемпотентности.</summary>
        public const string Idempotency = "idempotency";

        /// <summary>Тег для арендатора — инвалидирует все записи, ограниченные арендатором.</summary>
        public static string Tenant(string tenantId) => $"tenant:{tenantId}";

        /// <summary>Тег для пользователя — инвалидирует все записи, ограниченные пользователем.</summary>
        public static string User(string userId) => $"user:{userId}";
    }

    /// <summary>Ключ для списка разрешений конкретного пользователя.</summary>
    public static string UserPermissions(string userId) => $"perm:u:{userId}";

    /// <summary>Ключ для темы конкретного арендатора.</summary>
    public static string TenantTheme(string tenantId) => $"theme:t:{tenantId}";

    /// <summary>Ключ для общесистемной темы по умолчанию.</summary>
    public const string DefaultTheme = "theme:default";

    /// <summary>Ключ для записи идемпотентности, ограниченной арендатором.</summary>
    public static string IdempotencyEntry(string tenantId, string key) => $"idem:t:{tenantId}:{key}";

    /// <summary>
    /// Ключ для маркера отзыва разрешения на имперсонализацию, индексированный по JWT id.
    /// Читается при каждом аутентифицированном запросе, содержащем claim act_sub.
    /// </summary>
    public static string ImpersonationGrantStatus(string jti) => $"impgrant:{jti}";
}