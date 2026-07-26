namespace EDV.Framework.Web.Security;

public sealed class SecurityHeadersOptions
{
    /// <summary>
    /// Включает или отключает промежуточное ПО для заголовков безопасности.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Пути, которые следует пропускать (например, ресурсы OpenAPI/Scalar).
    /// </summary>
    public string[] ExcludedPaths { get; set; } = ["/scalar", "/openapi"];

    /// <summary>
    /// Разрешать ли встроенные стили в CSP (по умолчанию true для совместимости со Scalar).
    /// </summary>
    public bool AllowInlineStyles { get; set; } = true;

    /// <summary>
    /// Дополнительные источники скриптов для добавления в CSP.
    /// </summary>
    public string[] ScriptSources { get; set; } = [];

    /// <summary>
    /// Дополнительные источники стилей для добавления в CSP.
    /// </summary>
    public string[] StyleSources { get; set; } = [];
}