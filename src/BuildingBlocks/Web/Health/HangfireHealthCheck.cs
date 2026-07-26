using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EDV.Framework.Web.Health;

/// <summary>
/// Проверка здоровья, которая проверяет доступность хранилища Hangfire.
/// </summary>
public sealed class HangfireHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var storage = JobStorage.Current;
            using var connection = storage.GetConnection();
            return Task.FromResult(HealthCheckResult.Healthy("Хранилище Hangfire доступно."));
        }
#pragma warning disable CA1031 // Проверки здоровья должны перехватывать все исключения для сообщения о сниженной работоспособности/неисправности
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Хранилище Hangfire недоступно.", ex));
        }
#pragma warning restore CA1031
    }
}