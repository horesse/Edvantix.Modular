using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EDV.Framework.Web.Idempotency;

public static class Extensions
{
    /// <summary>
    /// Регистрирует параметры идемпотентности для использования в IdempotencyEndpointFilter.
    /// Применяйте к конкретным конечным точкам через расширение .WithIdempotency().
    /// </summary>
    public static IServiceCollection AddIdempotency(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<IdempotencyOptions>()
            .BindConfiguration(nameof(IdempotencyOptions));

        return services;
    }
}