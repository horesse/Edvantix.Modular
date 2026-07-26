using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Shared.Quota;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EDV.Framework.Quota;

/// <summary>
/// Счётчик квот на основе Redis. Ресурсы на основе счётчиков используют атомарную операцию <c>INCRBY</c> с ключом вида
/// <c>quota:{tenantId}:{resource}:{YYYYMM}</c> и временем жизни (TTL), истекающим вскоре после
/// окончания биллингового периода. Ресурсы на основе датчиков делегируют экземплярам <see cref="IQuotaGaugeProvider"/>,
/// которые модули регистрируют для сообщения текущего использования из своих собственных хранилищ состояния.
/// </summary>
public sealed class RedisQuotaService : IQuotaService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly QuotaOptions _options;
    private readonly QuotaPlanResolver _planResolver;
    private readonly IMultiTenantContextAccessor<AppTenantInfo>? _tenantAccessor;
    private readonly Dictionary<QuotaResource, IQuotaGaugeProvider> _gauges;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RedisQuotaService> _logger;

    public RedisQuotaService(
        IConnectionMultiplexer redis,
        QuotaOptions options,
        QuotaPlanResolver planResolver,
        IEnumerable<IQuotaGaugeProvider> gauges,
        TimeProvider timeProvider,
        ILogger<RedisQuotaService> logger,
        IMultiTenantContextAccessor<AppTenantInfo>? tenantAccessor = null)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(planResolver);
        ArgumentNullException.ThrowIfNull(gauges);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _redis = redis;
        _options = options;
        _planResolver = planResolver;
        _tenantAccessor = tenantAccessor;
        _timeProvider = timeProvider;
        _logger = logger;

        // Быстрое завершение при дублирующихся регистрациях датчиков — два провайдера для одного ресурса являются ошибкой.
        _gauges = gauges.ToDictionary(g => g.Resource);
    }

    public async ValueTask<QuotaCheckResult> CheckAsync(string tenantId, QuotaResource resource, long amount, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var (limit, exempt) = ResolveLimit(tenantId, resource);
        var current = await GetCurrentAsync(tenantId, resource, ct).ConfigureAwait(false);

        if (exempt || limit == long.MaxValue)
        {
            return QuotaCheckResult.Unlimited(resource, current);
        }

        var allowed = current + amount <= limit;
        return new QuotaCheckResult(allowed, resource, current, limit, GetPeriodResetUtc(resource));
    }

    public async ValueTask<long> RecordAsync(string tenantId, QuotaResource resource, long amount, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (!IsCounterResource(resource))
        {
            // Датчики считываются из состояния модуля; здесь нет счётчика для увеличения.
            return await GetCurrentAsync(tenantId, resource, ct).ConfigureAwait(false);
        }

        var db = _redis.GetDatabase();
        var key = BuildCounterKey(tenantId, resource);
        var newValue = await db.StringIncrementAsync(key, amount).ConfigureAwait(false);

        // Устанавливаем TTL, синхронизированный с границей периода, при первом обращении к ключу.
        // KeyExpireAsync ничего не делает, если у ключа уже есть TTL, поэтому безопасно вызывать при каждом увеличении.
        var reset = GetPeriodResetUtc(resource);
        if (reset is not null)
        {
            await db.KeyExpireAsync(key, reset.Value.UtcDateTime, ExpireWhen.HasNoExpiry).ConfigureAwait(false);
        }

        return newValue;
    }

    public async ValueTask<QuotaCheckResult> CheckAndRecordAsync(string tenantId, QuotaResource resource, long amount, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var (limit, exempt) = ResolveLimit(tenantId, resource);

        if (exempt || limit == long.MaxValue)
        {
            var after = await RecordAsync(tenantId, resource, amount, ct).ConfigureAwait(false);
            return QuotaCheckResult.Unlimited(resource, after);
        }

        if (!IsCounterResource(resource))
        {
            // Датчики не являются счётчиками — мы не можем "записать" их, поэтому делегируем CheckAsync.
            return await CheckAsync(tenantId, resource, amount, ct).ConfigureAwait(false);
        }

        var db = _redis.GetDatabase();
        var key = BuildCounterKey(tenantId, resource);
        var newValue = await db.StringIncrementAsync(key, amount).ConfigureAwait(false);
        var reset = GetPeriodResetUtc(resource);
        if (reset is not null)
        {
            await db.KeyExpireAsync(key, reset.Value.UtcDateTime, ExpireWhen.HasNoExpiry).ConfigureAwait(false);
        }

        if (newValue <= limit)
        {
            return new QuotaCheckResult(true, resource, newValue, limit, reset);
        }

        // Превышение: откатываем увеличение, чтобы повторные проверки не продолжали увеличивать счётчик.
        await db.StringIncrementAsync(key, -amount).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(
                "Квота превышена для арендатора {TenantId} ресурс {Resource}: {Current}/{Limit}",
                tenantId, resource, newValue, limit);
        }

        return new QuotaCheckResult(false, resource, newValue - amount, limit, reset);
    }

    public async ValueTask<long> GetCurrentAsync(string tenantId, QuotaResource resource, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (!IsCounterResource(resource))
        {
            if (_gauges.TryGetValue(resource, out var provider))
            {
                return await provider.GetCurrentAsync(tenantId, ct).ConfigureAwait(false);
            }

            return 0;
        }

        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(BuildCounterKey(tenantId, resource)).ConfigureAwait(false);
        return value.TryParse(out long parsed) ? parsed : 0;
    }

    private (long Limit, bool Exempt) ResolveLimit(string tenantId, QuotaResource resource)
    {
        if (_options.ExemptRootTenant && string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
        {
            return (long.MaxValue, true);
        }

        var tenant = _tenantAccessor?.MultiTenantContext?.TenantInfo;
        // Если аксессор разрешил арендатора, отличного от проверяемого (например, вызывающий
        // передал явный tenantId для межарендаторской операции), мы возвращаемся к настройкам тарифа по умолчанию.
        if (tenant is not null && !string.Equals(tenant.Id, tenantId, StringComparison.Ordinal))
        {
            tenant = null;
        }

        return (_planResolver.ResolveLimit(tenant, resource), false);
    }

    private static bool IsCounterResource(QuotaResource resource) => resource switch
    {
        QuotaResource.ApiCalls => true,
        QuotaResource.StorageBytes => true,
        _ => false
    };

    // Периодические счётчики сбрасываются на границе биллингового периода (ежемесячно).
    // Бессрочные счётчики (например, StorageBytes) накапливаются до явного уменьшения.
    private static bool IsPeriodic(QuotaResource resource) => resource switch
    {
        QuotaResource.ApiCalls => true,
        _ => false
    };

    private string BuildCounterKey(string tenantId, QuotaResource resource)
    {
        if (!IsPeriodic(resource))
        {
            return $"quota:{tenantId}:{resource}";
        }

        var now = _timeProvider.GetUtcNow();
        // Ежемесячный биллинговый период — наиболее полезное окно для SaaS; почасовые/ежедневные окна могут быть
        // добавлены как дополнительные значения QuotaResource при необходимости.
        var period = $"{now.Year:D4}{now.Month:D2}";
        return $"quota:{tenantId}:{resource}:{period}";
    }

    private DateTimeOffset? GetPeriodResetUtc(QuotaResource resource)
    {
        if (!IsPeriodic(resource))
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        // Сброс в первый момент следующего месяца UTC.
        var nextMonth = now.Month == 12
            ? new DateTimeOffset(now.Year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(now.Year, now.Month + 1, 1, 0, 0, 0, TimeSpan.Zero);
        return nextMonth;
    }
}