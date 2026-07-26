using EDV.Framework.Shared.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EDV.Framework.Persistence;

/// <summary>
/// Фоновый сервис, который логирует параметры конфигурации базы данных во время запуска приложения.
/// </summary>
public sealed class DatabaseOptionsStartupLogger : IHostedService
{
    private readonly ILogger<DatabaseOptionsStartupLogger> _logger;
    private readonly IOptions<DatabaseOptions> _options;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="DatabaseOptionsStartupLogger"/>.
    /// </summary>
    /// <param name="logger">Экземпляр логгера для записи информации о запуске.</param>
    /// <param name="options">Параметры конфигурации базы данных.</param>
    public DatabaseOptionsStartupLogger(
        ILogger<DatabaseOptionsStartupLogger> logger,
        IOptions<DatabaseOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// Логирует информацию о конфигурации базы данных при запуске сервиса.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены для прерывания операции.</param>
    /// <returns>Завершённая задача.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Текущий провайдер БД: {Provider}", options.Provider);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Не выполняет никаких операций при остановке сервиса.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены для прерывания операции.</param>
    /// <returns>Завершённая задача.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}