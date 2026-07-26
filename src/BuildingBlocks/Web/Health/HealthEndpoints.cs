using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EDV.Framework.Web.Health;

public static class HealthEndpoints
{
    public sealed record HealthResult(string Status, IEnumerable<HealthEntry> Results);
    public sealed record HealthEntry(string Name, string Status, string? Description, double DurationMs, Dictionary<string, object>? Details = default);
    
    public static IEndpointRouteBuilder MapDefaultHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/health")
                       .WithTags("Health")
                       .AllowAnonymous()
                       .DisableRateLimiting();


        // Живучесть: только процесс запущен (без внешних зависимостей)
        group.MapGet("/live",
                async Task<Ok<HealthResult>> (HealthCheckService hc, CancellationToken cancellationToken) =>
                {
                    var report = await hc.CheckHealthAsync(_ => false, cancellationToken);
                    var payload = new HealthResult(
                    Status: report.Status.ToString(),
                    Results: Array.Empty<HealthEntry>());

                    return TypedResults.Ok(payload);
                })
                .WithName("Liveness")
                .WithSummary("Быстрый зонд живучести процесса.")
                .WithDescription("Сообщает, жив ли процесс API. Не проверяет зависимости.")
                .Produces<HealthResult>(StatusCodes.Status200OK);

        // Готовность: включает БД и зарегистрированные проверки. Полная полезная нагрузка как при 200, так и при 503, чтобы
        // операторы видели, какая проверка не удалась; потребители зонда ориентируются на код состояния, поэтому тело 503 безопасно.
        group.MapGet("/ready",
                    async (HealthCheckService hc, CancellationToken cancellationToken) =>
                    {
                        var report = await hc.CheckHealthAsync(cancellationToken: cancellationToken);
                        var results = report.Entries.Select(e =>
                    new HealthEntry(
                        Name: e.Key,
                        Status: e.Value.Status.ToString(),
                        Description: e.Value.Description,
                        DurationMs: e.Value.Duration.TotalMilliseconds,
                        Details: e.Value.Data.ToDictionary(
                            k => k.Key,
                            v => v.Value is null ? "null" : v.Value
                        )));

                        var payload = new HealthResult(report.Status.ToString(), results);
                        var statusCode = report.Status == HealthStatus.Healthy
                            ? StatusCodes.Status200OK
                            : StatusCodes.Status503ServiceUnavailable;

                        return Results.Json(payload, statusCode: statusCode);
                    })
                    .WithName("Readiness")
                    .WithSummary("Зонд готовности с проверкой базы данных.")
                    .WithDescription("Возвращает 200, если все зависимости работают, иначе 503. Тело имеет одинаковую структуру в обоих случаях.")
                    .Produces<HealthResult>(StatusCodes.Status200OK)
                    .Produces<HealthResult>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}