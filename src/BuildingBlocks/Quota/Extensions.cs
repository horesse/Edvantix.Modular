using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace EDV.Framework.Quota;

public static class Extensions
{
    /// <summary>
    /// Регистрирует сервис квот. Использует счётчики на основе Redis, когда настроен <see cref="QuotaOptions.Redis"/>;
    /// в противном случае используется внутрипроцессный счётчик в памяти (только для разработки/тестов —
    /// не разделяется между экземплярами).
    /// </summary>
    public static IServiceCollection AddQuotas(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<QuotaOptions>()
            .BindConfiguration(nameof(QuotaOptions));

        var quotaOptions = configuration.GetSection(nameof(QuotaOptions)).Get<QuotaOptions>() ?? new QuotaOptions();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(quotaOptions);
        services.AddSingleton<QuotaPlanResolver>();

        if (!quotaOptions.Enabled)
        {
            services.AddScoped<IQuotaService, NoopQuotaService>();
            services.AddTransient<QuotaEnforcementMiddleware>();
            return services;
        }

        if (!string.IsNullOrWhiteSpace(quotaOptions.Redis))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var config = ConfigurationOptions.Parse(quotaOptions.Redis!);
                config.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(config);
            });

            // Scoped, чтобы провайдеры датчиков с зависимостями с ограниченным временем жизни (например, DbContext) разрешались на каждый запрос.
            services.AddScoped<IQuotaService, RedisQuotaService>();
        }
        else
        {
            services.AddSingleton<InMemoryQuotaStore>();
            services.AddScoped<IQuotaService, InMemoryQuotaService>();
        }

        services.AddTransient<QuotaEnforcementMiddleware>();

        return services;
    }

    /// <summary>
    /// Вставляет промежуточное ПО для проверки квот в конвейер. Должен выполняться после аутентификации
    /// (чтобы знать арендатора) и после ограничителя скорости (чтобы запросы, отклонённые по лимиту, не тратили квоту).
    /// Промежуточное ПО не выполняет действий, когда <see cref="QuotaOptions.Enabled"/> равно false.
    /// </summary>
    public static IApplicationBuilder UseQuotas(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<QuotaEnforcementMiddleware>();
    }
}