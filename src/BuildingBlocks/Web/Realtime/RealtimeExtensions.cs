using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EDV.Framework.Web.Realtime;

public static class RealtimeExtensions
{
    /// <summary>
    /// Регистрирует SignalR с бэкплейном Redis, когда настроен <c>CachingOptions:Redis</c>.
    /// Без Redis хаб продолжает работать в режиме одного хоста (полезно для тестов/разработки).
    /// Также регистрирует внутрипроцессный трекер присутствия, используемый хабом и конечной точкой присутствия.
    /// </summary>
    public static IServiceCollection AddRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var redis = configuration["CachingOptions:Redis"];
        var signalr = services.AddSignalR();
        if (!string.IsNullOrWhiteSpace(redis))
        {
            signalr.AddStackExchangeRedis(redis, options => options.Configuration.ChannelPrefix =
                StackExchange.Redis.RedisChannel.Literal("edv-signalr"));
        }

        services.AddSingleton<IPresenceTracker, PresenceTracker>();

        return services;
    }

    public static IEndpointRouteBuilder MapRealtime(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapHub<AppHub>("/api/v1/realtime/hub");

        // Конечная точка для снимка состояния — клиенты опрашивают её для получения начального состояния,
        // когда их сессия не получила рассылки PresenceChanged.
        endpoints.MapGet("/api/v1/realtime/presence",
                ([FromQuery] string userIds, IPresenceTracker presence) =>
                {
                    var ids = (userIds ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var map = presence.GetStatus(ids);
                    return Results.Ok(map.Select(kv => new { userId = kv.Key, online = kv.Value }));
                })
            .RequireAuthorization()
            .WithName("GetPresence")
            .WithSummary("Снимок статуса онлайн для списка идентификаторов пользователей через запятую.");

        return endpoints;
    }
}