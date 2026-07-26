using EDV.Framework.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EDV.Framework.Web.Idempotency;

/// <summary>
/// Фильтр конечной точки, обеспечивающий идемпотентность для запросов POST/PUT/PATCH.
/// Когда присутствует заголовок Idempotency-Key, ответ кэшируется и воспроизводится
/// для последующих запросов с тем же ключом.
/// </summary>
/// <remarks>
/// Использует <see cref="IDistributedCache"/> напрямую для пробного чтения (в обход
/// API HybridCache с обязательной фабрикой) и <see cref="HybridCache.SetAsync"/>
/// для записи, чтобы при воспроизведении использовались L1 и обычная инвалидация по тегам.
/// Использование <c>HybridCache</c> с <c>DisableUnderlyingData</c> как "только чтение-проба" является
/// известным анти-паттерном, отслеживаемым в dotnet/aspnetcore#57191.
/// </remarks>
public sealed class IdempotencyEndpointFilter : IEndpointFilter
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var httpContext = context.HttpContext;
        var options = httpContext.RequestServices.GetRequiredService<IOptions<IdempotencyOptions>>().Value;
        var idempotencyKey = httpContext.Request.Headers[options.HeaderName].ToString();

        // Нет заголовка = пропускаем (идемпотентность включается по запросу)
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return await next(context).ConfigureAwait(false);
        }

        if (idempotencyKey.Length > options.MaxKeyLength)
        {
            return TypedResults.BadRequest($"Ключ идемпотентности превышает максимальную длину {options.MaxKeyLength}.");
        }

        var distributedCache = httpContext.RequestServices.GetRequiredService<IDistributedCache>();
        var hybridCache = httpContext.RequestServices.GetRequiredService<HybridCache>();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<IdempotencyEndpointFilter>>();

        // Включаем контекст арендатора в ключ кэша для изоляции
        var tenantId = httpContext.User.FindFirst("tenant")?.Value ?? "global";
        var cacheKey = CacheKeys.IdempotencyEntry(tenantId, idempotencyKey);
        var tags = new[] { CacheKeys.Tags.Idempotency, CacheKeys.Tags.Tenant(tenantId) };

        // Пробное чтение только через IDistributedCache (реальный GetAsync, null при промахе — в отличие от фабрики HybridCache).
        // Обходит L1: воспроизведения редки по сравнению с первыми вызовами, поэтому прогретость L1 имеет мало значения.
        var cachedBytes = await distributedCache.GetAsync(cacheKey, httpContext.RequestAborted).ConfigureAwait(false);
        if (cachedBytes is not null && cachedBytes.Length > 0)
        {
            var cached = JsonSerializer.Deserialize<CachedIdempotentResponse>(cachedBytes, JsonOpts);
            if (cached is not null)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Идемпотентное воспроизведение для ключа {KeyHash}", HashKey(idempotencyKey));
                }
                httpContext.Response.Headers["Idempotency-Replayed"] = "true";
                httpContext.Response.StatusCode = cached.StatusCode;
                if (cached.ContentType is not null)
                {
                    httpContext.Response.ContentType = cached.ContentType;
                }

                if (cached.Body.Length > 0)
                {
                    await httpContext.Response.Body.WriteAsync(cached.Body, httpContext.RequestAborted).ConfigureAwait(false);
                }

                return null; // Ответ уже записан
            }
        }

        // Выполняем обработчик
        var result = await next(context).ConfigureAwait(false);

        // Кэшируем ответ через HybridCache, чтобы работала инвалидация по тегам для очисток.
        try
        {
            var body = result is not null ? JsonSerializer.SerializeToUtf8Bytes(result, JsonOpts) : [];
            var responseToCache = new CachedIdempotentResponse
            {
                StatusCode = httpContext.Response.StatusCode is > 0 and < 600 ? httpContext.Response.StatusCode : 200,
                ContentType = "application/json",
                Body = body
            };

            var setOptions = new HybridCacheEntryOptions
            {
                Expiration = options.DefaultTtl,
                LocalCacheExpiration = options.DefaultTtl < TimeSpan.FromMinutes(2) ? options.DefaultTtl : TimeSpan.FromMinutes(2),
            };
            await hybridCache.SetAsync(cacheKey, responseToCache, setOptions, tags, httpContext.RequestAborted).ConfigureAwait(false);
        }
        // Кэширование с максимальными усилиями: воспроизведение идемпотентности — это удобство, а не требование корректности
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Не удалось кэшировать идемпотентный ответ для ключа {KeyHash}", HashKey(idempotencyKey));
        }

        return result;
    }

    private static string HashKey(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }
}

public static class IdempotencyEndpointExtensions
{
    /// <summary>
    /// Включает идемпотентность для этой конечной точки. Требует заголовок Idempotency-Key в запросах.
    /// Повторные запросы с тем же ключом возвращают кэшированный ответ.
    /// </summary>
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter<IdempotencyEndpointFilter>();
    }
}