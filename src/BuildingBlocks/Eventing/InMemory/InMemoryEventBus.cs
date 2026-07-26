using EDV.Framework.Eventing.Abstractions;
using EDV.Framework.Eventing.Inbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;

namespace EDV.Framework.Eventing.InMemory;

/// <summary>
/// Реализация шины событий в памяти для однопроцессных развёртываний.
/// Разрешает обработчики через DI и опционально использует хранилище inbox для идемпотентности.
/// </summary>
public sealed partial class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;
    private readonly IEventTenantScope _tenantScope;

    // Закрытый тип интерфейса обработчика и его метод HandleAsync стабильны для каждого типа события,
    // поэтому они разрешаются один раз и кэшируются вместо повторного вычисления рефлексии на каждой публикации.
    private static readonly ConcurrentDictionary<Type, HandlerDispatch> DispatchCache = new();

    private readonly record struct HandlerDispatch(Type HandlerInterfaceType, MethodInfo HandleMethod);

    public InMemoryEventBus(IServiceProvider serviceProvider, ILogger<InMemoryEventBus> logger, IEventTenantScope tenantScope)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _tenantScope = tenantScope;
    }

    private static HandlerDispatch GetDispatch(Type eventType)
        => DispatchCache.GetOrAdd(eventType, static et =>
        {
            var handlerInterfaceType = typeof(IIntegrationEventHandler<>).MakeGenericType(et);
            var method = handlerInterfaceType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))
                ?? throw new InvalidOperationException($"IIntegrationEventHandler<{et.Name}> не объявляет HandleAsync.");
            return new HandlerDispatch(handlerInterfaceType, method);
        });

    public Task PublishAsync(IIntegrationEvent @event, CancellationToken ct = default)
        => PublishAsync(new[] { @event }, ct);

    public async Task PublishAsync(IEnumerable<IIntegrationEvent> events, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var @event in events)
        {
            await PublishSingleAsync(@event, ct).ConfigureAwait(false);
        }
    }

    private async Task PublishSingleAsync(IIntegrationEvent @event, CancellationToken ct)
    {
        var eventType = @event.GetType();
        LogPublishingEvent(eventType.FullName, @event.Id);

        var dispatch = GetDispatch(eventType);

        // Устанавливаем контекст арендатора ДО разрешения обработчиков — MultiTenantDbContext захватывает
        // TenantInfo в момент создания, поэтому поздняя установка арендатора приводит к NRE в фильтре запроса.
        // Именно это заставляет фоновых издателей работать корректно.
        using (_tenantScope.Begin(@event.TenantId))
        {
            using var scope = _serviceProvider.CreateScope();
            var provider = scope.ServiceProvider;

            var handlers = ResolveHandlers(provider, dispatch.HandlerInterfaceType);
            if (handlers.Length == 0)
            {
                LogNoHandlers(eventType.FullName);
                return;
            }

            var inbox = provider.GetService<IInboxStore>();

            foreach (var handler in handlers)
            {
                await InvokeHandlerAsync(handler, dispatch.HandleMethod, eventType, @event, inbox, ct).ConfigureAwait(false);
            }
        }
    }

    private static object[] ResolveHandlers(IServiceProvider provider, Type handlerInterfaceType)
        => provider.GetServices(handlerInterfaceType).Where(h => h is not null).ToArray()!;

    private async Task InvokeHandlerAsync(
        object handler,
        MethodInfo handleMethod,
        Type eventType,
        IIntegrationEvent @event,
        IInboxStore? inbox,
        CancellationToken ct)
    {
        var handlerName = handler.GetType().FullName ?? handler.GetType().Name;

        if (await ShouldSkipProcessedEventAsync(inbox, @event.Id, handlerName, ct).ConfigureAwait(false))
        {
            LogSkippingProcessed(@event.Id, handlerName);
            return;
        }

        await ExecuteHandlerAsync(handler, handleMethod, @event, eventType, handlerName, inbox, ct).ConfigureAwait(false);
    }

    private static async Task<bool> ShouldSkipProcessedEventAsync(IInboxStore? inbox, Guid eventId, string handlerName, CancellationToken ct)
    {
        return inbox != null && await inbox.HasProcessedAsync(eventId, handlerName, ct).ConfigureAwait(false);
    }

    private async Task ExecuteHandlerAsync(
        object handler,
        MethodInfo method,
        IIntegrationEvent @event,
        Type eventType,
        string handlerName,
        IInboxStore? inbox,
        CancellationToken ct)
    {
        try
        {
            var task = (Task)method.Invoke(handler, new object[] { @event, ct })!;
            await task.ConfigureAwait(false);

            if (inbox != null)
            {
                await inbox.MarkProcessedAsync(@event.Id, handlerName, @event.TenantId, eventType.AssemblyQualifiedName ?? eventType.FullName!, ct)
                    .ConfigureAwait(false);
            }
        }
        // Широкий catch намеренный: логируем и пробрасываем дальше, чтобы гарантировать
        // фиксацию всех сбоев обработчика независимо от типа исключения.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке интеграционного события {EventId} обработчиком {Handler}", @event.Id, handlerName);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Публикация интеграционного события {EventType} ({EventId})")]
    private partial void LogPublishingEvent(string? eventType, Guid eventId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Не зарегистрировано ни одного обработчика для интеграционного события типа {EventType}")]
    private partial void LogNoHandlers(string? eventType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Пропуск уже обработанного интеграционного события {EventId} для обработчика {Handler}")]
    private partial void LogSkippingProcessed(Guid eventId, string handler);
}
