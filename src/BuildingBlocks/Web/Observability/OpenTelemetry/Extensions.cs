using EDV.Framework.Caching.Telemetry;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using static EDV.Framework.Web.Observability.OpenTelemetry.OpenTelemetryOptions;

namespace EDV.Framework.Web.Observability.OpenTelemetry;

public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    
    public static IHostApplicationBuilder AddDefaultOpenTelemetry(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new OpenTelemetryOptions();
        builder.Configuration.GetSection(SectionName).Bind(options);

        if (!options.Enabled)
        {
            return builder;
        }

        builder.Services.AddOptions<OpenTelemetryOptions>()
            .BindConfiguration(SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Учитываем идентификацию оркестратора: Aspire (и любой OTLP-коллектор) внедряет OTEL_SERVICE_NAME как
        // имя ресурса, под которым он знает процесс (например, "edv-starter-api"). Переопределение его именем
        // сборки входа ("EDV.Starter.Api") разрывает корреляцию нашей телеметрии с этим ресурсом, поэтому панель
        // отображает процесс дважды. Используем внедрённое имя, когда оно присутствует; возвращаемся к ApplicationName,
        // если работаем отдельно.
        var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
            ?? builder.Environment.ApplicationName;

        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(serviceName: serviceName);

        // Общий ActivitySource для спанов (Mediator и др.)
        builder.Services.AddSingleton(new ActivitySource(builder.Environment.ApplicationName));

        // Aspire (и любой OTLP-коллектор) внедряет OTEL_EXPORTER_OTLP_ENDPOINT в процесс. Когда он присутствует, мы
        // экспортируем в него, даже если Exporter.Otlp.Enabled в конфигурации равно false, и позволяем SDK OpenTelemetry
        // считывать конечную точку/протокол из стандартных переменных окружения OTEL_EXPORTER_OTLP_* вместо переопределения
        // через конфигурацию — именно так телеметрия достигает вкладок Traces/Metrics панели Aspire (её OTLP-приёмник
        // находится на динамическом порту).
        var useEnvEndpoint = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

        ConfigureMetricsAndTracing(builder, options, resourceBuilder, serviceName, useEnvEndpoint);

        return builder;
    }

    private static void ConfigureMetricsAndTracing(
        IHostApplicationBuilder builder,
        OpenTelemetryOptions options,
        ResourceBuilder resourceBuilder,
        string serviceName,
        bool useEnvEndpoint)
    {
        var exportOtlp = options.Exporter.Otlp.Enabled || useEnvEndpoint;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                if (!options.Metrics.Enabled)
                {
                    return;
                }

                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddNpgsqlInstrumentation()
                    .AddRuntimeInstrumentation();

                // Применяем корзины гистограмм для длительности HTTP-сервера
                if (options.Http.Histograms.Enabled)
                {
                    metrics.AddView(
                        "http.server.duration",
                        new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = GetHistogramBuckets(options)
                        });
                }

                // Метрики строительного блока кэширования (попадания, промахи, длительность фабрики, инвалидации).
                metrics.AddMeter(CachingTelemetry.MeterName);

                // Метрики конвейера аудита (опубликовано, отброшено, сброс, мёртвая очередь).
                metrics.AddMeter("EDV.Modules.Auditing");

                // Метрики outbox событий (мёртвая очередь, повторная отправка) — строковый литерал соответствует
                // EventingTelemetry.MeterName; Web не ссылается на проект Eventing.
                metrics.AddMeter("EDV.Eventing");

                foreach (var meterName in options.Metrics.MeterNames ?? Array.Empty<string>())
                {
                    metrics.AddMeter(meterName);
                }

                if (exportOtlp)
                {
                    metrics.AddOtlpExporter((exporter, reader) =>
                    {
                        ConfigureOtlpExporter(options.Exporter.Otlp, exporter, useEnvEndpoint);

                        // Читатель метрик OTLP по умолчанию имеет интервал экспорта 60 секунд, поэтому после перезапуска
                        // метрики остаются пустыми целую минуту, пока логи и трассировка отображаются в течение секунд —
                        // это сбивающий с толку разрыв на панели Aspire. Учитываем стандартный OTEL_METRIC_EXPORT_INTERVAL,
                        // если задан, иначе экспортируем каждые 10 секунд: быстро локально, всё ещё разумно для
                        // производственного коллектора.
                        var intervalRaw = Environment.GetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL");
                        reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                            int.TryParse(intervalRaw, out var ms) && ms > 0 ? ms : 10_000;
                    });
                }
            })
            .WithTracing(tracing =>
            {
                if (!options.Tracing.Enabled)
                {
                    return;
                }

                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(instrumentation =>
                    {
                        instrumentation.Filter = context => !IsHealthCheck(context.Request.Path);
                        instrumentation.EnrichWithHttpRequest = EnrichWithHttpRequest;
                        instrumentation.EnrichWithHttpResponse = EnrichWithHttpResponse;
                    })
                    .AddHttpClientInstrumentation()
                    .AddNpgsql()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddRedisInstrumentation(redis =>
                    {
                        if (options.Data.FilterRedisCommands)
                        {
                            redis.SetVerboseDatabaseStatements = false;
                        }
                    })
                    .AddSource(builder.Environment.ApplicationName)
                    .AddSource("EDV.Hangfire")
                    .AddSource(CachingTelemetry.ActivitySourceName);

                if (exportOtlp)
                {
                    tracing.AddOtlpExporter(otlp =>
                    {
                        ConfigureOtlpExporter(options.Exporter.Otlp, otlp, useEnvEndpoint);
                    });
                }
            });

        // Спаны Mediator (опционально): добавляем поведение в DI для спанов конвейера.
        if (options.Mediator.Enabled)
        {
            builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(MediatorTracingBehavior<,>));
        }

        // Заглушка для Hangfire/инструментирования заданий: в настоящее время включено через Jobs.Enabled; подключение хуков в строительном блоке Jobs.
    }

    private static double[] GetHistogramBuckets(OpenTelemetryOptions options)
    {
        if (options.Http.Histograms.BucketBoundaries is { Length: > 0 } custom)
        {
            return custom;
        }

        // Корзины по умолчанию в секундах (от быстрых к медленным)
        return new[] { 0.01, 0.05, 0.1, 0.25, 0.5, 1, 2, 5 };
    }

    private static bool IsHealthCheck(PathString path) =>
        path.StartsWithSegments(HealthEndpointPath) ||
        path.StartsWithSegments(AlivenessEndpointPath);

    private static void EnrichWithHttpRequest(Activity activity, HttpRequest request)
    {
        activity.SetTag("http.method", request.Method);
        activity.SetTag("http.scheme", request.Scheme);
        activity.SetTag("http.host", request.Host.Value);
        activity.SetTag("http.target", request.Path);
    }

    private static void EnrichWithHttpResponse(Activity activity, HttpResponse response)
    {
        activity.SetTag("http.status_code", response.StatusCode);
    }

    private static void ConfigureOtlpExporter(
        OtlpOptions options,
        OtlpExporterOptions otlp,
        bool useEnvEndpoint)
    {
        if (useEnvEndpoint)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            otlp.Endpoint = new Uri(options.Endpoint);
        }

        var protocol = options.Protocol?.Trim().ToLowerInvariant();
        otlp.Protocol = protocol switch
        {
            "grpc" => OtlpExportProtocol.Grpc,
            "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
            _ => otlp.Protocol
        };
    }
}