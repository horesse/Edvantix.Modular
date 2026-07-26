using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace EDV.Framework.Web.Realtime;

/// <summary>
/// Единый общий хаб SignalR для общесистемного реалтайма: сообщения чата, индикаторы набора текста,
/// присутствие, уведомления. Модули не зависят от этого хаба напрямую — они отправляют через
/// <see cref="IHubContext{AppHub}"/> и нацелены на общеизвестные группы SignalR.
///
/// Соглашение об именах групп:
/// <list type="bullet">
///   <item><c>user:{userId}</c> — все соединения, открытые у пользователя. Используется для
///   кросс-канальных рассылок (уведомления, канал добавлен и т.д.).</item>
///   <item><c>channel:{channelId}</c> — все соединения каждого участника канала. Используется
///   для рассылки сообщений чата.</item>
/// </list>
/// </summary>
[Authorize]
public sealed class AppHub : Hub
{
    /// <summary>Окно ограничения для индикаторов набора текста на (канал, пользователь).</summary>
    private static readonly TimeSpan TypingThrottle = TimeSpan.FromSeconds(3);

    private readonly IChannelMembershipChecker _membership;
    private readonly IDistributedCache _cache;
    private readonly IUserChannelLookup _channels;
    private readonly IPresenceTracker _presence;
    private readonly ILogger<AppHub> _logger;

    public AppHub(
        IChannelMembershipChecker membership,
        IDistributedCache cache,
        IUserChannelLookup channels,
        IPresenceTracker presence,
        ILogger<AppHub> logger)
    {
        _membership = membership;
        _cache = cache;
        _channels = channels;
        _presence = presence;
        _logger = logger;
    }

    /// <summary>
    /// Считывает идентификатор аутентифицированного пользователя из участника подключения.
    /// Нельзя использовать <c>ICurrentUser</c> здесь, потому что он разрешается через
    /// <c>IHttpContextAccessor</c> — исходный <c>HttpContext</c> от negotiate не привязан
    /// к последующим вызовам методов хаба, поэтому любое обращение через него возвращает null.
    /// </summary>
    private string? GetUserId()
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated != true) return null;
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue("uid");
    }

    /// <summary>
    /// Считывает идентификатор арендатора из участника — используется для ограничения
    /// кросс-арендаторных рассылок (присутствие) одной группой арендатора, чтобы арендатор
    /// с 1000 пользователей не рассылал каждое подключение другим арендаторам.
    /// </summary>
    private string? GetTenantId()
    {
        var user = Context.User;
        if (user is null) return null;
        return user.FindFirstValue("tenant")
            ?? user.FindFirstValue("tid")
            ?? user.FindFirstValue("tenantId");
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId) || userId == Guid.Empty.ToString())
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}", Context.ConnectionAborted)
                .ConfigureAwait(false);

            // Присоединяемся к группе арендатора — ограничивает кросс-арендаторные рассылки
            // (присутствие), чтобы арендатор с 1000 пользователей не рассылал каждое подключение
            // другим арендаторам.
            var tenantId = GetTenantId();
            if (!string.IsNullOrEmpty(tenantId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}", Context.ConnectionAborted)
                    .ConfigureAwait(false);
            }

            var channelIds = await _channels
                .ListMyChannelIdsAsync(userId, Context.ConnectionAborted)
                .ConfigureAwait(false);

            foreach (var channelId in channelIds)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"channel:{channelId}", Context.ConnectionAborted)
                    .ConfigureAwait(false);
            }

            AppHubLog.Connected(_logger, Context.ConnectionId, userId, channelIds.Count);

            // При первом открытом соединении пользователя рассылаем PresenceChanged,
            // чтобы клиенты переключили индикатор. Ограничено группой арендатора, не Clients.All,
            // чтобы избежать глобальной рассылки.
            if (_presence.Connect(userId))
            {
                var target = string.IsNullOrEmpty(tenantId)
                    ? Clients.All
                    : Clients.Group($"tenant:{tenantId}");
                await target.SendAsync(
                        "PresenceChanged",
                        new { userId, online = true },
                        Context.ConnectionAborted)
                    .ConfigureAwait(false);
            }

            await base.OnConnectedAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            // Клиент отключился во время подключения (быстрое переподключение, навигация по странице,
            // churn negotiate/connect). Токен отмены прерывает выполняющиеся групповые присоединения /
            // поиск каналов. Подключения для настройки больше нет, поэтому это ожидаемо —
            // проглатываем, чтобы не создавать ошибку диспетчеризации хаба в логах.
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (!string.IsNullOrEmpty(userId) && _presence.Disconnect(userId))
        {
            var tenantId = GetTenantId();
            var target = string.IsNullOrEmpty(tenantId)
                ? Clients.All
                : Clients.Group($"tenant:{tenantId}");
            await target.SendAsync(
                    "PresenceChanged",
                    new { userId, online = false })
                .ConfigureAwait(false);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>
    /// Клиент вызывает <c>Typing(channelId)</c> во время набора текста. Ограничивается частотой
    /// раз в 3 секунды на (канал, пользователь) через распределённый кэш, чтобы активные UI
    /// не заливали сеть.
    /// </summary>
    public async Task Typing(Guid channelId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return;

        if (!await _membership.IsMemberAsync(channelId, userId, Context.ConnectionAborted).ConfigureAwait(false))
        {
            return;
        }

        var key = $"typing:{channelId}:{userId}";
        var existing = await _cache.GetStringAsync(key, Context.ConnectionAborted).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(existing)) return;

        await _cache.SetStringAsync(
                key,
                "1",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TypingThrottle },
                Context.ConnectionAborted)
            .ConfigureAwait(false);

        await Clients.OthersInGroup($"channel:{channelId}")
            .SendAsync("ChatTypingStarted", new { channelId, userId }, Context.ConnectionAborted)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Клиент вызывает <c>JoinChannel(channelId)</c>, когда открывает разговор.
    /// <see cref="OnConnectedAsync"/> предварительно присоединяет только те каналы, которые
    /// существовали — и в которых пользователь уже состоял — на момент подключения.
    /// Если канал/ЛС создан или членство предоставлено <em>после</em> установки сокета,
    /// иначе рассылки <c>channel:{id}</c> никогда не будут получены до перезагрузки страницы
    /// и нового подключения с повторным перечислением членств. Это присоединяет группу по
    /// требованию, проверяя членство так же, как и для набора текста. Идемпотентно —
    /// повторное присоединение к уже существующей группе не имеет эффекта, поэтому клиент
    /// может вызывать свободно при открытии и переподключении.
    /// </summary>
    public async Task JoinChannel(Guid channelId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return;

        if (!await _membership.IsMemberAsync(channelId, userId, Context.ConnectionAborted).ConfigureAwait(false))
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"channel:{channelId}", Context.ConnectionAborted)
            .ConfigureAwait(false);
    }
}

internal static partial class AppHubLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Подключение AppHub {ConnectionId} для пользователя {UserId} предварительно присоединило {ChannelCount} групп каналов")]
    public static partial void Connected(ILogger logger, string connectionId, string userId, int channelCount);
}