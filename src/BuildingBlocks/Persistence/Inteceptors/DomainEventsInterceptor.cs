using EDV.Framework.Core.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EDV.Framework.Persistence.Inteceptors;

/// <summary>
/// Перехватчик Entity Framework, который автоматически публикует доменные события после сохранения изменений.
/// </summary>
public sealed class DomainEventsInterceptor : SaveChangesInterceptor
{
    private readonly IPublisher _publisher;
    private readonly ILogger<DomainEventsInterceptor> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="DomainEventsInterceptor"/>.
    /// </summary>
    /// <param name="publisher">Издатель Mediator для публикации доменных событий.</param>
    /// <param name="logger">Логгер для отслеживания публикации доменных событий.</param>
    public DomainEventsInterceptor(IPublisher publisher, ILogger<DomainEventsInterceptor> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// Вызывается после сохранения изменений в базе данных. Публикует все доменные события из отслеживаемых сущностей.
    /// </summary>
    /// <param name="eventData">Контекстная информация о завершённой операции сохранения.</param>
    /// <param name="result">Количество записей состояния, записанных в базу данных.</param>
    /// <param name="cancellationToken">Токен отмены для прерывания операции.</param>
    /// <returns>Количество записей состояния, записанных в базу данных.</returns>
    /// <exception cref="ArgumentNullException">Выбрасывается, когда eventData равен null.</exception>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        var context = eventData.Context;
        if (context == null)
            return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);

        var domainEvents = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .SelectMany(e =>
            {
                var pending = e.Entity.DomainEvents.ToArray();
                e.Entity.ClearDomainEvents();
                return pending;
            })
            .ToArray();

        if (domainEvents.Length == 0)
            return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Публикация {Count} доменных событий...", domainEvents.Length);
        }

        foreach (var domainEvent in domainEvents)
        {
            try
            {
                await _publisher.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Сбои в обработчиках не должны приводить к откату уже зафиксированного сохранения
                // (события собираются после SaveChanges). Обработчикам, требующим гарантированной доставки,
                // следует использовать паттерн outbox.
                _logger.LogError(ex, "Не удалось опубликовать доменное событие {EventType}", domainEvent.GetType().Name);
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }
}