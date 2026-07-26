using EDV.Framework.Persistence.Inteceptors;
using EDV.Framework.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EDV.Framework.Persistence;

/// <summary>
/// Методы расширения для настройки сервисов сохраняемости и контекстов базы данных.
/// </summary>
public static class PersistenceExtensions
{
    /// <summary>
    /// Добавляет параметры конфигурации базы данных в коллекцию сервисов с проверкой.
    /// </summary>
    /// <param name="services">Коллекция сервисов, в которую добавляются параметры.</param>
    /// <param name="configuration">Экземпляр конфигурации, содержащий настройки базы данных.</param>
    /// <returns>Коллекция сервисов для цепочки вызовов.</returns>
    /// <exception cref="ArgumentNullException">Выбрасывается, когда configuration равен null.</exception>
    public static IServiceCollection AddDatabaseOptions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(nameof(DatabaseOptions)))
            .ValidateDataAnnotations()
            .Validate(o => !string.IsNullOrWhiteSpace(o.Provider), "DatabaseOptions.Provider обязателен.")
            .ValidateOnStart();
        services.AddHostedService<DatabaseOptionsStartupLogger>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ISaveChangesInterceptor, AuditableEntitySaveChangesInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DomainEventsInterceptor>();
        return services;
    }

    /// <summary>
    /// Добавляет настроенный Entity Framework DbContext в коллекцию сервисов.
    /// </summary>
    /// <typeparam name="TContext">Тип DbContext для настройки.</typeparam>
    /// <param name="services">Коллекция сервисов, в которую добавляется контекст.</param>
    /// <returns>Коллекция сервисов для цепочки вызовов.</returns>
    /// <exception cref="ArgumentNullException">Выбрасывается, когда services равен null.</exception>
    public static IServiceCollection AddDbContext<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<TContext>((sp, options) =>
        {
            var env = sp.GetRequiredService<IHostEnvironment>();
            var dbConfig = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.ConfigureDatabase(dbConfig.Provider, dbConfig.ConnectionString, dbConfig.MigrationsAssembly, env.IsDevelopment());
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
        });
        return services;
    }
}