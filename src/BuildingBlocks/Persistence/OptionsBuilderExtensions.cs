using EDV.Framework.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EDV.Framework.Persistence;

/// <summary>
/// Методы расширения для настройки DbContextOptionsBuilder в Entity Framework.
/// </summary>
public static class OptionsBuilderExtensions
{
    /// <summary>
    /// Настраивает провайдера базы данных и подключение для фреймворка.
    /// </summary>
    /// <param name="builder">Настраиваемый DbContextOptionsBuilder.</param>
    /// <param name="dbProvider">Провайдер базы данных (PostgreSQL, MSSQL).</param>
    /// <param name="connectionString">Строка подключения к базе данных.</param>
    /// <param name="migrationsAssembly">Сборка, содержащая миграции базы данных.</param>
    /// <param name="isDevelopment">Указывает, запущено ли приложение в режиме разработки.</param>
    /// <returns>Настроенный DbContextOptionsBuilder для цепочки вызовов.</returns>
    /// <exception cref="ArgumentNullException">Выбрасывается, когда builder равен null или dbProvider равен null или пустой строке.</exception>
    /// <exception cref="InvalidOperationException">Выбрасывается, когда указан неподдерживаемый провайдер базы данных.</exception>
    public static DbContextOptionsBuilder ConfigureDatabase(
        this DbContextOptionsBuilder builder,
        string dbProvider,
        string connectionString,
        string migrationsAssembly,
        bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(dbProvider);

        builder.ConfigureWarnings(warnings =>
            warnings.Log(RelationalEventId.PendingModelChangesWarning));

        switch (dbProvider.ToUpperInvariant())
        {
            case DbProviders.PostgreSQL:
                builder.UseNpgsql(connectionString, e =>
                {
                    e.MigrationsAssembly(migrationsAssembly);
                });
                break;

            default:
                throw new InvalidOperationException(
                    $"Провайдер базы данных {dbProvider} не поддерживается.");
        }

        if (isDevelopment)
        {
            builder.EnableSensitiveDataLogging();
            builder.EnableDetailedErrors();
        }

        return builder;
    }
}