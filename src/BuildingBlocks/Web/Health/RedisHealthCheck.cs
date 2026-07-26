using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EDV.Framework.Web.Health;

/// <summary>
/// Проверка здоровья, которая проверяет подключение к Redis путём выполнения цикла set/remove.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IDistributedCache _cache;

    public RedisHealthCheck(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            const string key = "__health_check__";
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
            };
            await _cache.SetStringAsync(key, "ok", options, cancellationToken).ConfigureAwait(false);
            await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("Redis доступен.");
        }
#pragma warning disable CA1031 // Проверки здоровья должны перехватывать все исключения для сообщения о сниженной работоспособности/неисправности
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis недоступен.", ex);
        }
#pragma warning restore CA1031
    }
}