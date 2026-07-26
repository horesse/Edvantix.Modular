using EDV.Framework.Eventing.Abstractions;
using System.Collections.Concurrent;
using System.Text.Json;

namespace EDV.Framework.Eventing.Serialization;

/// <summary>
/// Сериализатор интеграционных событий на основе System.Text.Json.
/// </summary>
public sealed class JsonEventSerializer : IEventSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // Разрешение типа события по имени (горячий путь для outbox/inbox) рефлективно разбирает
    // полное имя сборки и каждый раз сканирует загруженные сборки, поэтому результат для
    // каждого отдельного имени кэшируется здесь.
    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);

    public string Serialize(IIntegrationEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return JsonSerializer.Serialize(@event, @event.GetType(), Options);
    }

    public IIntegrationEvent? Deserialize(string payload, string eventTypeName)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(eventTypeName);

        var type = TypeCache.GetOrAdd(eventTypeName, static n => Type.GetType(n, throwOnError: false));
        if (type is null)
        {
            return null;
        }

        var result = JsonSerializer.Deserialize(payload, type, Options);
        return result as IIntegrationEvent;
    }
}
