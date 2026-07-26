using System.Diagnostics.Metrics;

namespace EDV.Framework.Eventing.Telemetry;

/// <summary>
/// Метрики OpenTelemetry для строительного блока событийности. Регистрируйте через
/// <c>metrics.AddMeter(EventingTelemetry.MeterName)</c> в настройке OTel.
/// </summary>
public static class EventingTelemetry
{
    /// <summary>Имя <see cref="Meter"/>, используемое для метрик событийности.</summary>
    public const string MeterName = "EDV.Eventing";

    internal static readonly Meter Meter = new(MeterName);

    /// <summary>Сообщения outbox, исчерпавшие попытки повтора и перемещённые в мёртвые письма.</summary>
    internal static readonly Counter<long> OutboxDeadLettered = Meter.CreateCounter<long>(
        "edv.eventing.outbox.deadlettered",
        unit: "{message}",
        description: "Количество сообщений outbox, перемещённых в мёртвые письма после исчерпания повторов.");

    /// <summary>Мёртвые сообщения outbox, восстановленные для новой попытки диспетчеризации.</summary>
    internal static readonly Counter<long> OutboxRedriven = Meter.CreateCounter<long>(
        "edv.eventing.outbox.redriven",
        unit: "{message}",
        description: "Количество мёртвых сообщений outbox, восстановленных для новой попытки.");
}
