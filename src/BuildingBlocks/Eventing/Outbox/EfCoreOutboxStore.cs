using EDV.Framework.Eventing.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EDV.Framework.Eventing.Outbox;

/// <summary>
/// Основанное на EF Core хранилище outbox для конкретного DbContext.
/// </summary>
/// <typeparam name="TDbContext">DbContext, которому принадлежит набор OutboxMessages.</typeparam>
public sealed class EfCoreOutboxStore<TDbContext> : IOutboxStore
    where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;
    private readonly IEventSerializer _serializer;
    private readonly ILogger<EfCoreOutboxStore<TDbContext>> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly EventingOptions _options;

    public EfCoreOutboxStore(
        TDbContext dbContext,
        IEventSerializer serializer,
        ILogger<EfCoreOutboxStore<TDbContext>> logger,
        TimeProvider timeProvider,
        IOptions<EventingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _dbContext = dbContext;
        _serializer = serializer;
        _logger = logger;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task AddAsync(IIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var payload = _serializer.Serialize(@event);
        var message = new OutboxMessage
        {
            Id = @event.Id,
            CreatedOnUtc = @event.OccurredOnUtc,
            Type = @event.GetType().AssemblyQualifiedName ?? @event.GetType().FullName!,
            Payload = payload,
            TenantId = @event.TenantId,
            CorrelationId = @event.CorrelationId,
            RetryCount = 0,
            IsDead = false
        };

        await _dbContext.Set<OutboxMessage>().AddAsync(message, ct).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingBatchAsync(int batchSize, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return await _dbContext.Set<OutboxMessage>()
            .Where(m => !m.IsDead && m.ProcessedOnUtc == null && (m.NextRetryAt == null || m.NextRetryAt <= now))
            .OrderBy(m => m.CreatedOnUtc)
            .Take(batchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task MarkAsProcessedAsync(OutboxMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        message.ProcessedOnUtc = _timeProvider.GetUtcNow().UtcDateTime;
        _dbContext.Set<OutboxMessage>().Update(message);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task MarkAsFailedAsync(OutboxMessage message, string error, bool isDead, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        message.RetryCount++;
        message.LastError = error;
        message.IsDead = isDead;
        // Разносим повторы по экспоненциальному backoff, чтобы постоянно падающее сообщение
        // не срабатывало на каждом цикле диспетчеризации. Мёртвое сообщение больше не повторяется,
        // поэтому оставляем его доступным как null.
        message.NextRetryAt = isDead ? null : _timeProvider.GetUtcNow().UtcDateTime.Add(BackoffFor(message.RetryCount));
        _dbContext.Set<OutboxMessage>().Update(message);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetDeadLetteredAsync(int max, CancellationToken ct = default)
    {
        if (max <= 0) max = 100;
        return await _dbContext.Set<OutboxMessage>()
            .Where(m => m.IsDead)
            .OrderBy(m => m.CreatedOnUtc)
            .Take(max)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<int> RedriveDeadLettersAsync(IReadOnlyCollection<Guid>? ids, CancellationToken ct = default)
    {
        var query = _dbContext.Set<OutboxMessage>().Where(m => m.IsDead);
        if (ids is { Count: > 0 })
        {
            query = query.Where(m => ids.Contains(m.Id));
        }

        var dead = await query.ToListAsync(ct).ConfigureAwait(false);
        foreach (var message in dead)
        {
            message.IsDead = false;
            message.RetryCount = 0;
            message.LastError = null;
            message.NextRetryAt = null;
        }

        if (dead.Count > 0)
        {
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            Telemetry.EventingTelemetry.OutboxRedriven.Add(dead.Count);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Восстановлено {Count} мёртвых(ое) сообщений(е) outbox для новой попытки.", dead.Count);
            }
        }

        return dead.Count;
    }

    private TimeSpan BackoffFor(int retryCount)
    {
        var baseSeconds = _options.OutboxRetryBaseDelaySeconds > 0 ? _options.OutboxRetryBaseDelaySeconds : 30;
        var maxSeconds = _options.OutboxRetryMaxDelaySeconds > 0 ? _options.OutboxRetryMaxDelaySeconds : 3600;
        // retryCount равен 1 после первого сбоя → первый backoff равен точно baseSeconds.
        var exponent = Math.Min(retryCount - 1, 30); // ограничиваем, чтобы сдвиг не переполнился
        var seconds = Math.Min((double)baseSeconds * Math.Pow(2, exponent), maxSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
