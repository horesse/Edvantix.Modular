using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace EDV.Framework.Web.FeatureFlags;

public static class Extensions
{
    /// <summary>
    /// Добавляет управление функциями с tenant-ориентированными фильтрами функций.
    /// Считывает флаги функций из секции конфигурации "FeatureManagement".
    /// </summary>
    public static IServiceCollection AddFeatureFlags(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddFeatureManagement(configuration.GetSection("FeatureManagement"))
            .AddFeatureFilter<TenantFeatureFilter>();

        return services;
    }
}