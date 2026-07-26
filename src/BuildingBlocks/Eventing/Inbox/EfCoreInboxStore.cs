using Microsoft.EntityFrameworkCore;

namespace EDV.Framework.Eventing.Inbox;

/// <summary>
/// Основанное на EF Core хранилище inbox для конкретного DbContext.
/// </summary>
/// <typeparam name="TDbContext">DbContext, которому принадлежит набор InboxMessages.</typeparam>
public sealed class EfCoreInboxStore<TDbContext> : IInboxStore
    where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public EfCoreInboxStore(TDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<bool> HasProcessedAsync(Guid eventId, string handlerName, CancellationToken ct = default)
    {
        return await _dbContext.Set<InboxMessage>()
            .AnyAsync(i => i.Id == eventId && i.HandlerName == handlerName, ct)
            .ConfigureAwait(false);
    }

    public async Task MarkProcessedAsync(Guid eventId, string handlerName, string? tenantId, string eventType, CancellationToken ct = default)
    {
        // Идемпотентность: пропускаем, если уже отмечено (гонка между прямой публикацией и повтором outbox)
        bool alreadyProcessed = await _dbContext.Set<InboxMessage>()
            .AnyAsync(i => i.Id == eventId && i.HandlerName == handlerName, ct)
            .ConfigureAwait(false);

        if (alreadyProcessed)
        {
            return;
        }

        var message = new InboxMessage
        {
            Id = eventId,
            EventType = eventType,
            HandlerName = handlerName,
            TenantId = tenantId,
            ProcessedOnUtc = _timeProvider.GetUtcNow().UtcDateTime
        };

        _dbContext.Set<InboxMessage>().Add(message);

        try
        {
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException) when (!ct.IsCancellationRequested)
        {
            // Гонку выиграла параллельная вставка — считаем событие уже обработанным.
            _dbContext.ChangeTracker.Clear();
        }
    }
}
