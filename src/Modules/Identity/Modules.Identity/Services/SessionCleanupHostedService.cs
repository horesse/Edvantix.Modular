using EDV.Modules.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Identity.Services;

/// <summary>
/// Фоновый сервис, периодически очищающий истёкшие сессии.
/// Запускается каждый час и удаляет сессии, истёкшие более 30 дней назад.
/// </summary>
public sealed class SessionCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionCleanupHostedService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly int _retentionDays = 30;

    public SessionCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionCleanupHostedService> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Сервис очистки сессий запущен");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                await CleanupExpiredSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Ожидаемо при завершении работы
                break;
            }
            catch (Exception ex)
            {
                // Цикл очистки не должен ронять хост — сбои повторяются на следующем интервале
                _logger.LogError(ex, "Ошибка при очистке сессий");
            }
        }

        _logger.LogInformation("Сервис очистки сессий остановлен");
    }

    private async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        // cutoffDate = now - retentionDays, поэтому ExpiresAt < cutoffDate уже подразумевает ExpiresAt < now.
        var cutoffDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-_retentionDays);
        var deleted = await db.UserSessions
            .Where(s => s.ExpiresAt < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Очищено {Count} истёкших сессий", deleted);
        }
    }
}