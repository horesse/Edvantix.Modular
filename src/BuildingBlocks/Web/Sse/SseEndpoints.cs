using EDV.Framework.Core.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Framework.Web.Sse;

public static class SseEndpoints
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Сопоставляет конечную точку обмена токенами SSE (<c>POST /api/v1/sse/token</c>, аутентификация через JWT) и
    /// конечную точку потока данных (<c>GET /api/v1/sse/stream?token=&lt;guid&gt;</c>). API EventSource браузера
    /// не может отправлять заголовок Authorization, поэтому поток аутентифицируется через кратковременный непрозрачный
    /// токен, выпущенный конечной точкой токена.
    /// </summary>
    public static IEndpointRouteBuilder MapSseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/api/v1/sse/token", async (
            ICurrentUser currentUser,
            ISseTokenService tokens,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.IsAuthenticated())
            {
                return Results.Unauthorized();
            }

            var userId = currentUser.GetUserId().ToString();
            var tenantId = currentUser.GetTenant();
            var token = await tokens.IssueAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { token });
        })
        .WithName("SseToken")
        .WithSummary("Выпуск кратковременного токена для SSE-потока")
        .WithTags("SSE")
        .RequireAuthorization();

        endpoints.MapGet("/api/v1/sse/stream", async (
            HttpContext context,
            [Microsoft.AspNetCore.Mvc.FromQuery] Guid token,
            ISseTokenService tokens,
            SseConnectionManager connectionManager,
            CancellationToken cancellationToken) =>
        {
            var principal = await tokens.ConsumeAsync(token, cancellationToken).ConfigureAwait(false);
            if (principal is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            // Нет `Connection: keep-alive` — это заголовок hop-by-hop, запрещённый для HTTP/2+ (RFC 9113 §8.2.2),
            // поэтому Kestrel удаляет его и предупреждает при каждом SSE-подключении (поток работает через HTTP/2 через ALPN).
            // В любом случае он был избыточен: HTTP/1.1 по умолчанию сохраняет соединения открытыми.
            context.Response.Headers["X-Accel-Buffering"] = "no"; // отключить буферизацию nginx

            var (connectionId, reader) = connectionManager.Connect(principal.UserId, principal.TenantId);

            // Немедленно отправляем заголовки ответа и начальный комментарий. Kestrel буферизирует
            // заголовки ответа до первой записи тела, а наша первая запись могла бы быть сигналом
            // heartbeat через HeartbeatInterval (15 секунд) — поэтому промис fetch() клиента
            // (который разрешается при получении заголовков) висел бы в ожидании, и интерфейс показывал бы
            // "подключение" до 15 секунд при каждом подключении/переподключении. Запись незначительного SSE-комментария
            // сейчас отправляет заголовки и позволяет клиенту сразу переключиться в состояние "подключено".
            await context.Response.WriteAsync(":connected\n\n", cancellationToken).ConfigureAwait(false);
            await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

            using var heartbeat = new PeriodicTimer(HeartbeatInterval);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var waitTask = reader.WaitToReadAsync(cancellationToken).AsTask();
                    var tickTask = heartbeat.WaitForNextTickAsync(cancellationToken).AsTask();

                    var completed = await Task.WhenAny(waitTask, tickTask).ConfigureAwait(false);

                    if (completed == tickTask)
                    {
                        _ = await tickTask.ConfigureAwait(false);
                        await context.Response.WriteAsync(":heartbeat\n\n", cancellationToken).ConfigureAwait(false);
                        await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var hasData = await waitTask.ConfigureAwait(false);
                    if (!hasData)
                    {
                        break;
                    }

                    while (reader.TryRead(out var sseEvent))
                    {
                        await WriteEventAsync(context.Response, sseEvent, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Клиент отключился — ожидаемо.
            }
            finally
            {
                connectionManager.Disconnect(connectionId);
            }
        })
        .WithName("SseStream")
        .WithSummary("Поток Server-Sent Events (аутентификация через ?token=, полученный от /sse/token)")
        .WithTags("SSE")
        .AllowAnonymous()
        .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task WriteEventAsync(HttpResponse response, SseEvent sseEvent, CancellationToken ct)
    {
        if (sseEvent.Id is not null)
        {
            await response.WriteAsync($"id: {sseEvent.Id}\n", ct).ConfigureAwait(false);
        }

        await response.WriteAsync($"event: {sseEvent.EventType}\n", ct).ConfigureAwait(false);

        // Спецификация SSE: многострочные данные требуют префикса "data: " для каждой строки
        foreach (var line in sseEvent.Data.Split('\n'))
        {
            await response.WriteAsync($"data: {line}\n", ct).ConfigureAwait(false);
        }

        await response.WriteAsync("\n", ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }
}