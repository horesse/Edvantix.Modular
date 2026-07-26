namespace EDV.Framework.Caching;

/// <summary>
/// Конфигурация для строительного блока кэширования на основе HybridCache.
/// Привязывается из секции конфигурации <c>CachingOptions</c>.
/// </summary>
public sealed class CachingOptions
{
    /// <summary>Строка подключения к Redis. Если пусто, распределённый кэш L2 возвращается к внутрипроцессному.</summary>
    public string Redis { get; set; } = string.Empty;

    /// <summary>
    /// Включить SSL/TLS для подключения к Redis. Если null, используется значение по умолчанию из строки подключения.
    /// Aspire 13.x по умолчанию использует TLS для Redis на основном порту; установите <c>false</c> при подключении через
    /// конечную точку с простым TCP (см. <c>AppHost.cs</c>).
    /// </summary>
    public bool? EnableSsl { get; set; }

    /// <summary>
    /// Общее время жизни записи кэша как для L1 (внутрипроцессный), так и для L2 (Redis).
    /// Применяется как значение по умолчанию, когда вызывающий не передаёт <c>HybridCacheEntryOptions</c>.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Время жизни внутрипроцессной копии L1. Устанавливается коротким, чтобы ограничить рассинхронизацию между узлами после
    /// <c>RemoveAsync</c>/<c>RemoveByTagAsync</c> на соседнем узле, поскольку HybridCache не имеет
    /// встроенного L1-бэкплейна. См. <c>docs/caching.md</c> для выбора компромисса.
    /// </summary>
    public TimeSpan DefaultLocalCacheExpiration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Максимальная длина ключа, принимаемая HybridCache. Ключи длиннее этого значения отклоняются.</summary>
    public int MaximumKeyLength { get; set; } = 1024;

    /// <summary>Максимальный размер сериализованной полезной нагрузки в байтах. Записи, превышающие этот размер, молча пропускаются (логируются).</summary>
    public long MaximumPayloadBytes { get; set; } = 1024 * 1024; // 1 МБ
}