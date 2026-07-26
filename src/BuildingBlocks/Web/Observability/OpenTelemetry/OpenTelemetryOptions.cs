using System.ComponentModel.DataAnnotations;

namespace EDV.Framework.Web.Observability.OpenTelemetry;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetryOptions";

    /// <summary>
    /// Глобальный переключатель включения/отключения OpenTelemetry.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public TracingOptions Tracing { get; set; } = new();

    public MetricsOptions Metrics { get; set; } = new();

    public ExporterOptions Exporter { get; set; } = new();

    /// <summary>
    /// Параметры инструментирования заданий (например, Hangfire).
    /// </summary>
    public JobOptions Jobs { get; set; } = new();

    /// <summary>
    /// Параметры инструментирования конвейера Mediator.
    /// </summary>
    public MediatorOptions Mediator { get; set; } = new();

    /// <summary>
    /// Параметры инструментирования HTTP (включая гистограммы).
    /// </summary>
    public HttpOptions Http { get; set; } = new();

    /// <summary>
    /// Параметры фильтрации инструментирования EF/Redis.
    /// </summary>
    public DataOptions Data { get; set; } = new();

    public sealed class TracingOptions
    {
        public bool Enabled { get; set; } = true;
    }

    public sealed class MetricsOptions
    {
        public bool Enabled { get; set; } = true;
        public string[]? MeterNames { get; set; }
    }

    public sealed class ExporterOptions
    {
        public OtlpOptions Otlp { get; set; } = new();
    }

    public sealed class OtlpOptions
    {
        public bool Enabled { get; set; } = true;

        [Url]
        public string? Endpoint { get; set; }

        /// <summary>
        /// Транспортный протокол, например "grpc" или "http/protobuf".
        /// </summary>
        public string? Protocol { get; set; }
    }

    public sealed class JobOptions
    {
        /// <summary>Включить трассировку/метрики для заданий (например, Hangfire).</summary>
        public bool Enabled { get; set; } = true;
    }

    public sealed class MediatorOptions
    {
        /// <summary>Включить спаны вокруг команд/запросов Mediator.</summary>
        public bool Enabled { get; set; } = true;
    }

    public sealed class HttpOptions
    {
        public HistogramOptions Histograms { get; set; } = new();

        public sealed class HistogramOptions
        {
            /// <summary>Включить гистограммы длительности HTTP-запросов.</summary>
            public bool Enabled { get; set; } = true;

            /// <summary>Пользовательские границы корзин (в секундах). Если null/пусто, применяются значения по умолчанию.</summary>
            public double[]? BucketBoundaries { get; set; }
        }
    }

    public sealed class DataOptions
    {
        /// <summary>Подавлять SQL-текст в инструментировании EF для снижения PII/шума.</summary>
        public bool FilterEfStatements { get; set; } = true;

        /// <summary>Подавлять текст команд Redis в инструментировании для снижения шума.</summary>
        public bool FilterRedisCommands { get; set; } = true;
    }

}