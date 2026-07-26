using EDV.Framework.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EDV.Framework.Jobs;

/// <summary>
/// Очистка устаревших блокировок Hangfire от упавших экземпляров с максимальными усилиями.
/// Выполняется как BackgroundService, поэтому никогда не блокирует запуск приложения.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Создаётся DI через AddHostedService")]
internal sealed class HangfireStaleLockCleanupService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<HangfireStaleLockCleanupService> _logger;

    public HangfireStaleLockCleanupService(
        IConfiguration configuration,
        ILogger<HangfireStaleLockCleanupService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Небольшая задержка, чтобы Hangfire сначала инициализировал свою схему
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);

        var dbOptions = _configuration.GetSection(nameof(DatabaseOptions)).Get<DatabaseOptions>();
        if (dbOptions is null || !dbOptions.Provider.Equals(DbProviders.PostgreSQL, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await using var connection = new NpgsqlConnection(dbOptions.ConnectionString);
            await connection.OpenAsync(stoppingToken).ConfigureAwait(false);

            await using var cmd = new NpgsqlCommand(
                "DELETE FROM hangfire.lock WHERE acquired < NOW() - INTERVAL '5 minutes'",
                connection);

            int deleted = await cmd.ExecuteNonQueryAsync(stoppingToken).ConfigureAwait(false);
            if (deleted > 0)
            {
                _logger.LogWarning("Очищено {Count} устаревших блокировок Hangfire", deleted);
            }
        }
        // Очистка с максимальными усилиями: таблица может ещё не существовать при первом запуске или БД может быть временно недоступна
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Не удалось очистить устаревшие блокировки Hangfire (таблица может ещё не существовать)");
        }
    }
}