using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace EDV.Framework.Web.Sse;

public sealed record SsePrincipal(string UserId, string? TenantId);

public interface ISseTokenService
{
    Task<Guid> IssueAsync(string userId, string? tenantId, CancellationToken cancellationToken);

    Task<SsePrincipal?> ConsumeAsync(Guid token, CancellationToken cancellationToken);
}

/// <summary>
/// Кратковременный одноразовый токен для аутентификации SSE-потоков. API EventSource браузера не может
/// добавлять заголовки Authorization, поэтому клиенты обменивают свой JWT в /sse/token на непрозрачный токен,
/// а затем открывают поток по адресу /sse/stream?token=&lt;guid&gt;. Токен удаляется при первом использовании и
/// истекает через 30 секунд в противном случае. Использует IDistributedCache (Redis в production) —
/// одноразовые токены не получают выгоды от L1 HybridCache, и IDistributedCache является правильным
/// примитивом, поскольку нам нужна семантика реального чтения-или-промаха без заполненных фабрикой null.
/// </summary>
internal sealed class SseTokenService(IDistributedCache cache) : ISseTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromSeconds(30);

    private static readonly DistributedCacheEntryOptions EntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TokenLifetime,
    };

    public async Task<Guid> IssueAsync(string userId, string? tenantId, CancellationToken cancellationToken)
    {
        var token = Guid.CreateVersion7();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new SsePrincipal(userId, tenantId));
        await cache.SetAsync(KeyFor(token), payload, EntryOptions, cancellationToken).ConfigureAwait(false);
        return token;
    }

    public async Task<SsePrincipal?> ConsumeAsync(Guid token, CancellationToken cancellationToken)
    {
        var key = KeyFor(token);
        var payload = await cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (payload is null || payload.Length == 0)
        {
            return null;
        }

        await cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<SsePrincipal>(payload);
    }

    private static string KeyFor(Guid token) => $"sse:tok:{token:N}";
}