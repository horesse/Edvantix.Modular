using System.Collections.Concurrent;

namespace EDV.Framework.Storage.Local;

/// <summary>
/// Хранилище в памяти для кратковременных токенов загрузки, используемое как запасной вариант
/// для локального хранилища в разработке. Продакшн-развёртывания используют S3 — этот тип нужен
/// для того, чтобы dev/test-окружения без MinIO тоже работали. Синглтон; токены одноразовые.
/// </summary>
public sealed class LocalPresignTokenStore
{
    private readonly ConcurrentDictionary<string, LocalPresignToken> _tokens = new(StringComparer.Ordinal);

    public string Issue(string storageKey, string contentType, long maxBytes, TimeSpan ttl)
    {
        var token = Guid.NewGuid().ToString("N");
        _tokens[token] = new LocalPresignToken(storageKey, contentType, maxBytes, DateTimeOffset.UtcNow.Add(ttl));
        return token;
    }

    public LocalPresignToken? Consume(string token)
    {
        if (!_tokens.TryRemove(token, out var entry))
        {
            return null;
        }
        return entry.ExpiresAt < DateTimeOffset.UtcNow ? null : entry;
    }
}

public sealed record LocalPresignToken(string StorageKey, string ContentType, long MaxBytes, DateTimeOffset ExpiresAt);
