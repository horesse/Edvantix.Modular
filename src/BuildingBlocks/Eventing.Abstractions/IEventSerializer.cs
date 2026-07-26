namespace EDV.Framework.Eventing.Abstractions;

/// <summary>
/// Сериализует и десериализует интеграционные события для транспорта и хранения (outbox).
/// </summary>
public interface IEventSerializer
{
    string Serialize(IIntegrationEvent @event);

    IIntegrationEvent? Deserialize(string payload, string eventTypeName);
}