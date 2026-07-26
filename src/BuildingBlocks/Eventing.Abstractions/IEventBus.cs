namespace EDV.Framework.Eventing.Abstractions;

/// <summary>
/// Абстракция над шиной событий. Начальный провайдер — in-memory; дополнительные провайдеры
/// могут быть добавлены без изменения модулей, которые публикуют или обрабатывают события.
/// </summary>
public interface IEventBus
{
    Task PublishAsync(IIntegrationEvent @event, CancellationToken ct = default);

    Task PublishAsync(IEnumerable<IIntegrationEvent> events, CancellationToken ct = default);
}