using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Finbuckle.MultiTenant.Abstractions;
using EDV.Framework.Quota;
using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Storage.Local;
using EDV.Framework.Storage.S3;
using EDV.Framework.Storage.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EDV.Framework.Storage;

public static class Extensions
{
    public static IServiceCollection AddLocalFileStorage(this IServiceCollection services)
    {
        services.AddScoped<IStorageService, LocalStorageService>();
        return services;
    }

    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = configuration["Storage:Provider"]?.ToLowerInvariant();
        var quotaEnabled = configuration
            .GetSection(nameof(QuotaOptions))
            .Get<QuotaOptions>()?.Enabled == true;

        if (string.Equals(provider, "s3", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<S3StorageOptions>(configuration.GetSection("Storage:S3"));

            services.AddSingleton<IAmazonS3>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<S3StorageOptions>>().Value;

                if (string.IsNullOrWhiteSpace(options.Bucket))
                {
                    throw new InvalidOperationException("Storage:S3:Bucket обязателен при использовании хранилища S3.");
                }

                var config = new AmazonS3Config();

                if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
                {
                    // S3-совместимый эндпоинт (например, MinIO). Обычно требуется path-style
                    // адресация, потому что такие сервисы не маршрутизируют виртуально-хостовые
                    // поддомены бакетов.
                    config.ServiceURL = options.ServiceUrl;
                    config.ForcePathStyle = options.ForcePathStyle;

                    // SDK всё равно требует регион аутентификации для SigV4, даже при обращении
                    // к пользовательскому эндпоинту.
                    config.AuthenticationRegion = string.IsNullOrWhiteSpace(options.Region) ? "us-east-1" : options.Region;
                }
                else if (!string.IsNullOrWhiteSpace(options.Region))
                {
                    config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
                }

                var hasExplicitCredentials = !string.IsNullOrWhiteSpace(options.AccessKey)
                    && !string.IsNullOrWhiteSpace(options.SecretKey);

                return hasExplicitCredentials
                    ? new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config)
                    : new AmazonS3Client(config);
            });

            services.AddTransient<S3StorageService>();
            RegisterStorageService<S3StorageService>(services, quotaEnabled, ServiceLifetime.Transient);
        }
        else
        {
            services.AddScoped<LocalStorageService>();
            RegisterStorageService<LocalStorageService>(services, quotaEnabled, ServiceLifetime.Scoped);
        }

        return services;
    }

    private static void RegisterStorageService<TInner>(
        IServiceCollection services,
        bool quotaEnabled,
        ServiceLifetime innerLifetime)
        where TInner : class, IStorageService
    {
        if (quotaEnabled)
        {
            // Время жизни декоратора — scoped, потому что IQuotaService разрешается на запрос.
            services.AddScoped<IStorageService>(sp => new QuotaMeteredStorageService(
                sp.GetRequiredService<TInner>(),
                sp.GetRequiredService<IQuotaService>(),
                sp.GetRequiredService<IMultiTenantContextAccessor<AppTenantInfo>>(),
                sp.GetRequiredService<ILogger<QuotaMeteredStorageService>>()));
            return;
        }

        services.Add(new ServiceDescriptor(
            typeof(IStorageService),
            sp => sp.GetRequiredService<TInner>(),
            innerLifetime));
    }
}
