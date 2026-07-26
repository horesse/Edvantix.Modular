using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EDV.Framework.Web.HttpResilience;

public static class Extensions
{
    /// <summary>
    /// Добавляет стандартный обработчик устойчивости (повторы, автоматический выключатель, таймаут) в построитель HTTP-клиента.
    /// Конфигурация считывается из секции "HttpResilienceOptions".
    /// </summary>
    public static IHttpClientBuilder AddEdvResilience(this IHttpClientBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(nameof(HttpResilienceOptions)).Get<HttpResilienceOptions>() ?? new HttpResilienceOptions();

        if (!options.Enabled)
        {
            return builder;
        }

        builder.AddStandardResilienceHandler(pipeline =>
        {
            pipeline.Retry.MaxRetryAttempts = options.MaxRetryAttempts;
            pipeline.Retry.Delay = options.MedianFirstRetryDelay;

            pipeline.TotalRequestTimeout.Timeout = options.TotalTimeout;
            pipeline.AttemptTimeout.Timeout = options.AttemptTimeout;

            pipeline.CircuitBreaker.BreakDuration = options.CircuitBreakerBreakDuration;
            pipeline.CircuitBreaker.FailureRatio = options.CircuitBreakerFailureRatio;
            pipeline.CircuitBreaker.MinimumThroughput = options.CircuitBreakerMinimumThroughput;
        });

        return builder;
    }
}