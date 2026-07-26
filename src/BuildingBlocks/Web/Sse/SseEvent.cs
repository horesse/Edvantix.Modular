namespace EDV.Framework.Web.Sse;

/// <summary>
/// Представляет событие Server-Sent Events для отправки подключённым клиентам.
/// </summary>
/// <param name="EventType">Тип события (соответствует полю SSE 'event:').</param>
/// <param name="Data">Данные события (соответствуют полю SSE 'data:'). Обычно JSON.</param>
/// <param name="Id">Необязательный идентификатор события для отслеживания переподключений клиента.</param>
public sealed record SseEvent(string EventType, string Data, string? Id = null);