using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Sinks.OpenTelemetry;

namespace EDV.Framework.Web.Observability.Logging.Serilog;

public static class Extensions
{
    public static IHostApplicationBuilder AddDefaultLogging(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<HttpRequestContextEnricher>();

        // Разрешаем экспорт логов в OTLP один раз (env-var/config), чтобы снижение добавлялось только при наличии конечной точки.
        var otlp = ResolveOtlpLogExport(builder);

        builder.Services.AddSerilog((context, logger) =>
        {
            var httpEnricher = context.GetRequiredService<HttpRequestContextEnricher>();
            logger.ReadFrom.Configuration(builder.Configuration);
            logger.Enrich.With(httpEnricher);
            logger
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Error)
                .MinimumLevel.Override("Hangfire", LogEventLevel.Warning)
                .MinimumLevel.Override("Finbuckle.MultiTenant", LogEventLevel.Warning)
                .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware"));

            // Отправляем структурированные логи через OTLP (например, панель .NET Aspire / compose collector), когда конечная точка
            // доступна. Serilog управляет конвейером логирования и НЕ передаёт логи другим провайдерам ILogger, поэтому
            // экспортёр логов OpenTelemetry SDK не видит эти события — мы экспортируем из Serilog. Аналогично
            // автоматическому определению трассировки/метрик в AddEdvOpenTelemetry: внедрённый OTEL_EXPORTER_OTLP_ENDPOINT (Aspire)
            // имеет приоритет, иначе используется настроенная конечная точка экспортёра, когда Exporter.Otlp.Enabled равно true.
            if (otlp is not null)
            {
                logger.WriteTo.OpenTelemetry(sink =>
                {
                    sink.Endpoint = otlp.Endpoint;
                    sink.Protocol = otlp.Protocol;
                    // service.name должен совпадать с ресурсом трассировки/метрик (AddEdvOpenTelemetry разрешает тот же
                    // OTEL_SERVICE_NAME ?? ApplicationName), чтобы панель группировала логи под тем же ресурсом, что и
                    // спаны, к которым они относятся — и принимала имя ресурса оркестратора (например, Aspire
                    // "edv-starter-api"), а не имя сборки входа, которая отображала бы процесс дважды.
                    sink.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
                            ?? builder.Environment.ApplicationName
                    };
                    // Приёмнику OTLP Aspire требуется внедрённый заголовок x-otlp-api-key. SDK OTel считывает
                    // OTEL_EXPORTER_OTLP_HEADERS автоматически для трассировки/метрик; этот снипет не делает этого, поэтому передаём его явно.
                    if (otlp.Headers.Count > 0)
                    {
                        sink.Headers = otlp.Headers;
                    }
                });
            }
        });
        return builder;
    }

    private sealed record OtlpLogExport(string Endpoint, OtlpProtocol Protocol, IDictionary<string, string> Headers);

    private static OtlpLogExport? ResolveOtlpLogExport(IHostApplicationBuilder builder)
    {
        // Учитываем глобальный переключатель OpenTelemetry, соответствующий шлюзу для трассировки/метрик.
        if (!builder.Configuration.GetValue("OpenTelemetryOptions:Enabled", true))
        {
            return null;
        }

        var envEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        string? endpoint;
        string? protocolRaw;
        if (!string.IsNullOrWhiteSpace(envEndpoint))
        {
            // Внедрённая конечная точка (Aspire / коллектор) имеет приоритет и экспортирует, даже если в конфигурации Otlp отключён.
            endpoint = envEndpoint;
            protocolRaw = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
        }
        else if (builder.Configuration.GetValue("OpenTelemetryOptions:Exporter:Otlp:Enabled", false))
        {
            endpoint = builder.Configuration["OpenTelemetryOptions:Exporter:Otlp:Endpoint"];
            protocolRaw = builder.Configuration["OpenTelemetryOptions:Exporter:Otlp:Protocol"];
        }
        else
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        var protocol = protocolRaw?.Trim().ToLowerInvariant() switch
        {
            "http/protobuf" => OtlpProtocol.HttpProtobuf,
            _ => OtlpProtocol.Grpc
        };

        // gRPC использует базовую конечную точку как есть; для HTTP снипет Serilog ожидает полный путь сигнала.
        if (protocol == OtlpProtocol.HttpProtobuf &&
            !endpoint.Contains("/v1/logs", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = $"{endpoint.TrimEnd('/')}/v1/logs";
        }

        var headers = ParseOtlpHeaders(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS"));
        return new OtlpLogExport(endpoint, protocol, headers);
    }

    // Разбирает формат пар ключ/значение через запятую, используемый в OTEL_EXPORTER_OTLP_HEADERS.
    private static Dictionary<string, string> ParseOtlpHeaders(string? raw)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return headers;
        }

        foreach (var pair in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = pair.IndexOf('=', StringComparison.Ordinal);
            if (idx <= 0)
            {
                continue;
            }

            var key = pair[..idx].Trim();
            if (key.Length > 0)
            {
                headers[key] = pair[(idx + 1)..].Trim();
            }
        }

        return headers;
    }
}