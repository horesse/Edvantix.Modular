using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace EDV.Framework.Web.Sse;

/// <summary>
/// Управляет активными SSE-соединениями, ключи которых — уникальный <see cref="Guid"/> на каждое
/// соединение, чтобы один пользователь с несколькими вкладками сохранял все потоки открытыми.
/// Поддерживает адресную отправку (по userId — рассылает всем активным соединениям пользователя)
/// и широковещательные рассылки по арендатору. Потокобезопасен через ConcurrentDictionary.
/// </summary>
public sealed class SseConnectionManager
{
    private readonly ConcurrentDictionary<Guid, Connection> _connections = new();
    private readonly ILogger<SseConnectionManager> _logger;

    public SseConnectionManager(ILogger<SseConnectionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Регистрирует новое соединение и возвращает стабильный connectionId + читатель канала,
    /// который будет использоваться конечной точкой.
    /// </summary>
    public (Guid ConnectionId, ChannelReader<SseEvent> Reader) Connect(string userId, string? tenantId = null)
    {
        var connectionId = Guid.CreateVersion7();
        var channel = Channel.CreateBounded<SseEvent>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        _connections[connectionId] = new Connection(userId, tenantId, channel);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("SSE клиент подключён: connection={ConnectionId} user={UserId} tenant={TenantId}",
                connectionId, userId, tenantId ?? "none");
        }

        return (connectionId, channel.Reader);
    }

    /// <summary>
    /// Отключает конкретное соединение и завершает его канал.
    /// </summary>
    public void Disconnect(Guid connectionId)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            connection.Channel.Writer.TryComplete();

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("SSE клиент отключён: connection={ConnectionId} user={UserId}",
                    connectionId, connection.UserId);
            }
        }
    }

    /// <summary>
    /// Отправляет событие всем соединениям, принадлежащим указанному пользователю (все вкладки, все устройства).
    /// Возвращает количество каналов, в которые событие было записано.
    /// </summary>
    public int TrySend(string userId, SseEvent sseEvent)
    {
        var sent = 0;
        foreach (var (_, connection) in _connections)
        {
            if (string.Equals(connection.UserId, userId, StringComparison.Ordinal)
                && connection.Channel.Writer.TryWrite(sseEvent))
            {
                sent++;
            }
        }

        return sent;
    }

    /// <summary>
    /// Широковещательно отправляет событие всем соединениям в указанном арендаторе.
    /// </summary>
    public int Broadcast(string tenantId, SseEvent sseEvent)
    {
        var sent = 0;
        foreach (var (_, connection) in _connections)
        {
            if (string.Equals(connection.TenantId, tenantId, StringComparison.Ordinal)
                && connection.Channel.Writer.TryWrite(sseEvent))
            {
                sent++;
            }
        }

        return sent;
    }

    /// <summary>
    /// Широковещательно отправляет событие всем подключённым клиентам (кросс-арендаторно).
    /// </summary>
    public int BroadcastAll(SseEvent sseEvent)
    {
        var sent = 0;
        foreach (var (_, connection) in _connections)
        {
            if (connection.Channel.Writer.TryWrite(sseEvent))
            {
                sent++;
            }
        }

        return sent;
    }

    /// <summary>Количество активных соединений всех пользователей.</summary>
    public int ActiveConnections => _connections.Count;

    private sealed record Connection(string UserId, string? TenantId, Channel<SseEvent> Channel);
}