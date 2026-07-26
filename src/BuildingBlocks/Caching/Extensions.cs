using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace EDV.Framework.Caching;

/// <summary>
/// Расширения DI для строительного блока кэширования на основе HybridCache.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Регистрирует <see cref="HybridCache"/> с двухуровневой архитектурой: Redis (если задан
    /// <see cref="CachingOptions.Redis"/>) или внутрипроцессный распределённый кэш как запасной вариант,
    /// затем оборачивает его в <see cref="ObservableHybridCache"/>, чтобы каждая операция отправляла
    /// метрики и активности OTel через <see cref="Telemetry.CachingTelemetry"/>.
    /// </summary>
    /// <remarks>
    /// HybridCache обеспечивает защиту от бронирования через <c>GetOrCreateAsync</c>, встроенную
    /// двухуровневость L1 (внутрипроцессный) + L2 (распределённый) и логическую инвалидацию по тегам.
    /// Потребители внедряют <see cref="HybridCache"/> напрямую — декоратор прозрачен.
    /// </remarks>
    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<CachingOptions>()
            .BindConfiguration(nameof(CachingOptions));

        var cacheOptions = configuration.GetSection(nameof(CachingOptions)).Get<CachingOptions>() ?? new CachingOptions();

        // L2: Redis, если настроен, иначе внутрипроцессный распределённый кэш. StackExchangeRedis 9.0+
        // реализует IBufferDistributedCache, который HybridCache использует для чтения без копирования.
        if (string.IsNullOrEmpty(cacheOptions.Redis))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            // Подключаемся один раз и используем мультиплексор совместно с кэшем Redis, хранилищем ключей
            // Data Protection и будущими потребителями — один пул подключений на хост, а не на функцию.
            var redisConfig = ConfigurationOptions.Parse(cacheOptions.Redis);
            redisConfig.AbortOnConnectFail = false;
            if (cacheOptions.EnableSsl.HasValue)
            {
                redisConfig.Ssl = cacheOptions.EnableSsl.Value;
            }
            var sharedMultiplexer = ConnectionMultiplexer.Connect(redisConfig);
            services.AddSingleton<IConnectionMultiplexer>(sharedMultiplexer);

            services.AddStackExchangeRedisCache(options =>
            {
                options.ConnectionMultiplexerFactory = () =>
                    Task.FromResult<IConnectionMultiplexer>(sharedMultiplexer);
            });

            // Сохраняем ключи Data Protection (cookie аутентификации, токены сброса/подтверждения, антифорж) в
            // Redis, чтобы многозвенные хосты использовали общее кольцо ключей и токены сохранялись при перезапусках.
            services.AddDataProtection()
                .PersistKeysToStackExchangeRedis(sharedMultiplexer, "DataProtection-Keys")
                .SetApplicationName("EDV.Starter");
        }

        // HybridCache автоматически компонуется с любым IDistributedCache, зарегистрированным выше.
        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = cacheOptions.DefaultExpiration,            // Общее время жизни L1 + L2
                LocalCacheExpiration = cacheOptions.DefaultLocalCacheExpiration, // Только L1
            };
            options.MaximumKeyLength = cacheOptions.MaximumKeyLength;
            options.MaximumPayloadBytes = cacheOptions.MaximumPayloadBytes;
        });

        // Оборачиваем HybridCache в декоратор с OTel-метриками: захватываем дескриптор,
        // установленный AddHybridCache, удаляем его и регистрируем фабрику, которая создаёт
        // внутренний экземпляр и возвращает наш обёрнутый.
        DecorateHybridCache(services);

        return services;
    }

    private static void DecorateHybridCache(IServiceCollection services)
    {
        var originalDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(HybridCache))
            ?? throw new InvalidOperationException("HybridCache не зарегистрирован. Вызов AddHybridCache должен предшествовать DecorateHybridCache.");

        services.Remove(originalDescriptor);

        services.AddSingleton<HybridCache>(sp =>
        {
            HybridCache inner;
            if (originalDescriptor.ImplementationInstance is HybridCache instance)
            {
                inner = instance;
            }
            else if (originalDescriptor.ImplementationFactory is { } factory)
            {
                inner = (HybridCache)factory(sp);
            }
            else
            {
                inner = (HybridCache)ActivatorUtilities.CreateInstance(sp, originalDescriptor.ImplementationType!);
            }

            return new ObservableHybridCache(inner);
        });
    }
}