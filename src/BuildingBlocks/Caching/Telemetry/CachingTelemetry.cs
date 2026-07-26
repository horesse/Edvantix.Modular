using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EDV.Framework.Caching.Telemetry;

/// <summary>
/// Примитивы OpenTelemetry для строительного блока кэширования.
/// Предоставляются как статические поля, чтобы их можно было подключить к конвейеру OTel через
/// <c>metrics.AddMeter(CachingTelemetry.MeterName)</c> и
/// <c>tracing.AddSource(CachingTelemetry.ActivitySourceName)</c>.
/// </summary>
public static class CachingTelemetry
{
    /// <summary>Имя <see cref="ActivitySource"/>, используемое для спанов кэша.</summary>
    public const string ActivitySourceName = "EDV.Caching";

    /// <summary>Имя <see cref="Meter"/>, используемое для метрик кэша.</summary>
    public const string MeterName = "EDV.Caching";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    internal static readonly Meter Meter = new(MeterName);

    /// <summary>Попадание в кэш L1 или L2 — фабрика не вызывалась.</summary>
    internal static readonly Counter<long> Hits = Meter.CreateCounter<long>(
        "edv.cache.hits",
        unit: "{hit}",
        description: "Количество чтений из кэша, которые вернули значение без вызова фабрики.");

    /// <summary>Промах кэша — фабрика была вызвана для получения свежего значения.</summary>
    internal static readonly Counter<long> Misses = Meter.CreateCounter<long>(
        "edv.cache.misses",
        unit: "{miss}",
        description: "Количество чтений из кэша, которые вызвали фабрику, потому что запись отсутствовала или была признана недействительной.");

    /// <summary>Явные удаления — <c>RemoveAsync</c> или <c>RemoveByTagAsync</c>.</summary>
    internal static readonly Counter<long> Invalidations = Meter.CreateCounter<long>(
        "edv.cache.invalidations",
        unit: "{invalidation}",
        description: "Количество явных удалений из кэша (RemoveAsync + RemoveByTagAsync).");

    /// <summary>Длительность выполнения фабрики — записывается только при промахе кэша.</summary>
    internal static readonly Histogram<double> FactoryDurationMs = Meter.CreateHistogram<double>(
        "edv.cache.factory.duration",
        unit: "ms",
        description: "Длительность вызова фабрики при промахе кэша.");
}