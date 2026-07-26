using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EDV.Framework.Eventing.Outbox;

/// <summary>
/// Фоновый сервис, периодически диспетчеризующий сообщения outbox.
/// Альтернатива планированию через Hangfire для более простых развёртываний.
/// </summary>
public sealed partial class OutboxDispatcherHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcherHostedService> _logger;
    private readonly TimeSpan _interval;

    public OutboxDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<EventingOptions> options,
        ILogger<OutboxDispatcherHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(options.Value.OutboxDispatchIntervalSeconds > 0
            ? options.Value.OutboxDispatchIntervalSeconds
            : 10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarted(_interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOutboxAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Штатное завершение работы
                break;
            }
            // Широкий catch намеренный: цикл хостед-сервиса не должен падать из-за
            // временных ошибок; сбои логируются, а следующий цикл повторяет попытку.
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка диспетчеризации сообщений outbox");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Хостед-сервис диспетчера outbox остановлен");
    }

    private async Task DispatchOutboxAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
        await dispatcher.DispatchAsync(ct).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Хостед-сервис диспетчера outbox запущен. Интервал диспетчеризации: {Interval}с")]
    private partial void LogServiceStarted(double interval);
}
