using System.Diagnostics.Metrics;

namespace EDV.Modules.Auditing.Core;

/// <summary>
/// Инструменты OpenTelemetry для конвейера аудита. Представлены как статические
/// поля, чтобы их было дёшево использовать откуда угодно на горячем пути —
/// без обращения к DI, без аллокаций. Подключаются к экспортёру OTel через
/// <c>metrics.AddMeter(AuditingTelemetry.MeterName)</c>.
/// </summary>
public static class AuditingTelemetry
{
    public const string MeterName = "EDV.Modules.Auditing";

    internal static readonly Meter Meter = new(MeterName);

    /// <summary>Успешная публикация в канал (без учёта отброшенных).</summary>
    internal static readonly Counter<long> Published = Meter.CreateCounter<long>(
        "edv.audit.published",
        unit: "{event}",
        description: "Количество событий аудита, принятых публикатором канала.");

    /// <summary>
    /// Канал был полон, и политика ограниченного канала вытеснила более старое событие,
    /// чтобы освободить место. Используйте этот счётчик для оповещения при устойчивом
    /// давлении — ненулевая скорость в течение минут сигнализирует, что sink не успевает.
    /// </summary>
    internal static readonly Counter<long> Dropped = Meter.CreateCounter<long>(
        "edv.audit.dropped",
        unit: "{event}",
        description: "Количество событий аудита, отброшенных из-за насыщения канала.");

    /// <summary>Успешный flush пакета в sink.</summary>
    internal static readonly Counter<long> Flushed = Meter.CreateCounter<long>(
        "edv.audit.flushed",
        unit: "{event}",
        description: "Количество событий аудита, успешно записанных sink'ом.");

    /// <summary>
    /// Flush пакета в sink выбросил исключение — считает события, которые будут
    /// повторены (если только попытки повтора не исчерпаны, в этом случае также
    /// увеличивается <see cref="DeadLettered"/>).
    /// </summary>
    internal static readonly Counter<long> FlushFailed = Meter.CreateCounter<long>(
        "edv.audit.flush_failed",
        unit: "{batch}",
        description: "Количество пакетов sink'а аудита, которые не удалось записать.");

    /// <summary>
    /// События, которые исчерпали попытки повтора и были записаны в журнал
    /// мёртвых писем. Должно всегда вызывать оповещение при пороге = 1.
    /// </summary>
    internal static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>(
        "edv.audit.dead_lettered",
        unit: "{event}",
        description: "Количество событий аудита, записанных в sink мёртвых писем после исчерпания повторов.");

    /// <summary>Сквозная задержка flush sink'а, включая повторы.</summary>
    internal static readonly Histogram<double> FlushDurationMs = Meter.CreateHistogram<double>(
        "edv.audit.flush.duration",
        unit: "ms",
        description: "Длительность flush sink'а, включая повторы.");
}
