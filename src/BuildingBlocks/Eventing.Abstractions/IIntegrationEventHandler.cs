namespace EDV.Framework.Eventing.Abstractions;

/// <summary>
/// Обрабатывает один тип интеграционного события.
/// </summary>
/// <typeparam name="TEvent">Тип интеграционного события.</typeparam>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}