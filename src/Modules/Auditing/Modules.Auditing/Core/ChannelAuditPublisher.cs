using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Auditing.Contracts;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Threading.Channels;

namespace EDV.Modules.Auditing.Core;

/// <summary>
/// Неблокирующий публикатор с двумя полосами: высокопропускная полоса по умолчанию
/// (вытесняет старые под давлением) и полоса безопасности комплаенс-уровня
/// (ограниченная, но создаёт back-pressure при заполнении). Обе полосы вычитываются
/// одним фоновым воркером, который отдаёт приоритет полосе безопасности.
/// </summary>
public sealed class ChannelAuditPublisher : IAuditPublisher
{
    private static readonly IAuditScope DefaultScope = new DefaultAuditScope(null, null, null, null, null, null, null, null, AuditTag.None);
    private readonly Channel<AuditEnvelope> _default;
    private readonly Channel<AuditEnvelope> _security;
    private readonly int _defaultCapacity;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMultiTenantContextAccessor<AppTenantInfo> _tenantAccessor;
    private readonly TimeProvider _timeProvider;

    public IAuditScope CurrentScope =>
        _httpContextAccessor.HttpContext?.RequestServices.GetService(typeof(IAuditScope)) as IAuditScope
        ?? DefaultScope;

    public ChannelAuditPublisher(
        IHttpContextAccessor httpContextAccessor,
        IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
        TimeProvider timeProvider,
        int capacity = 50_000,
        int securityCapacity = 50_000)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantAccessor = tenantAccessor;
        _timeProvider = timeProvider;
        _defaultCapacity = capacity;

        // Полоса по умолчанию: вытесняет старые, чтобы задержка оставалась предсказуемой
        // под давлением. Приемлемо для событий activity / entity-change.
        _default = Channel.CreateBounded<AuditEnvelope>(new BoundedChannelOptions(capacity)
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        // Полоса безопасности: никогда не вытесняет. Если sink застрял, публикаторы
        // испытывают back-pressure, пока очередь не разгрузится. Здесь идут события
        // комплаенс-уровня (результаты входа, изменения прав, имперсонация).
        _security = Channel.CreateBounded<AuditEnvelope>(new BoundedChannelOptions(securityCapacity)
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async ValueTask PublishAsync(IAuditEvent auditEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var scope = CurrentScope;
        var envelope = CreateEnvelope(auditEvent);
        envelope = BackfillScopeContext(envelope, scope);
        envelope = BackfillAmbientContext(envelope);

        var typeTag = new KeyValuePair<string, object?>("event_type", envelope.EventType.ToString());
        AuditingTelemetry.Published.Add(1, typeTag);

        if (envelope.EventType == AuditEventType.Security)
        {
            // WriteAsync ожидает, когда канал безопасности заполнен — в этом весь смысл
            // этой полосы: мы скорее замедлим запрос, чем отбросим значимое для комплаенса событие.
            await _security.Writer.WriteAsync(envelope, ct).ConfigureAwait(false);
            return;
        }

        // DropOldest никогда не возвращает false из TryWrite, поэтому приблизительно оцениваем
        // отбросы, сравнивая глубину чтения с ёмкостью перед записью — неточно при множестве
        // писателей, но важна именно скорость.
        if (_default.Reader.Count >= _defaultCapacity)
        {
            AuditingTelemetry.Dropped.Add(1, typeTag);
        }
        _default.Writer.TryWrite(envelope);
    }

    private AuditEnvelope CreateEnvelope(IAuditEvent auditEvent)
    {
        if (auditEvent is AuditEnvelope existing)
        {
            return existing;
        }

        return new AuditEnvelope(
            id: Guid.CreateVersion7(),
            occurredAtUtc: auditEvent.OccurredAtUtc,
            receivedAtUtc: _timeProvider.GetUtcNow().UtcDateTime,
            eventType: auditEvent.EventType,
            severity: auditEvent.Severity,
            tenantId: auditEvent.TenantId,
            userId: auditEvent.UserId,
            userName: auditEvent.UserName,
            traceId: auditEvent.TraceId,
            spanId: auditEvent.SpanId,
            correlationId: auditEvent.CorrelationId,
            requestId: auditEvent.RequestId,
            source: auditEvent.Source,
            tags: auditEvent.Tags,
            payload: auditEvent.Payload);
    }

    private static AuditEnvelope BackfillScopeContext(AuditEnvelope env, IAuditScope scope)
    {
        bool needsTenantBackfill = string.IsNullOrWhiteSpace(env.TenantId);
        bool needsUserBackfill = string.IsNullOrWhiteSpace(env.UserId) && scope.UserId is not null;

        if (!needsTenantBackfill && !needsUserBackfill)
        {
            return env;
        }

        return new AuditEnvelope(
            id: env.Id,
            occurredAtUtc: env.OccurredAtUtc,
            receivedAtUtc: env.ReceivedAtUtc,
            eventType: env.EventType,
            severity: env.Severity,
            tenantId: needsTenantBackfill ? scope.TenantId : env.TenantId,
            userId: needsUserBackfill ? scope.UserId : env.UserId,
            userName: needsUserBackfill ? scope.UserName ?? env.UserName : env.UserName,
            traceId: env.TraceId,
            spanId: env.SpanId,
            correlationId: env.CorrelationId,
            requestId: env.RequestId,
            source: env.Source,
            tags: env.Tags,
            payload: env.Payload);
    }

    /// <summary>
    /// Обогащение последней инстанции для конвертов, публикуемых вне HTTP-запроса —
    /// как правило, перехватчик SaveChanges, работающий внутри задания Hangfire. Читает
    /// арендатора из окружающего Finbuckle-аксессора и данные трассировки из
    /// <see cref="Activity.Current"/>; атрибуция пользователя остаётся такой, какую
    /// предоставила область (<c>ICurrentUser</c>, устанавливаемый активатором, — scoped,
    /// поэтому публикатор его не видит).
    /// </summary>
    private AuditEnvelope BackfillAmbientContext(AuditEnvelope env)
    {
        bool needTenant = string.IsNullOrWhiteSpace(env.TenantId);
        bool needTrace = string.IsNullOrWhiteSpace(env.TraceId);
        bool needSpan = string.IsNullOrWhiteSpace(env.SpanId);

        if (!needTenant && !needTrace && !needSpan) return env;

        var ambientTenant = needTenant
            ? _tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            : null;
        var activity = Activity.Current;

        return new AuditEnvelope(
            id: env.Id,
            occurredAtUtc: env.OccurredAtUtc,
            receivedAtUtc: env.ReceivedAtUtc,
            eventType: env.EventType,
            severity: env.Severity,
            tenantId: needTenant ? ambientTenant ?? env.TenantId : env.TenantId,
            userId: env.UserId,
            userName: env.UserName,
            traceId: needTrace ? activity?.TraceId.ToString() ?? env.TraceId : env.TraceId,
            spanId: needSpan ? activity?.SpanId.ToString() ?? env.SpanId : env.SpanId,
            correlationId: env.CorrelationId,
            requestId: env.RequestId,
            source: env.Source,
            tags: env.Tags,
            payload: env.Payload);
    }

    /// <summary>Читатель полосы по умолчанию. Вычитывается воркером вторым.</summary>
    internal ChannelReader<AuditEnvelope> Reader => _default.Reader;

    /// <summary>Читатель полосы безопасности. Вычитывается воркером первым.</summary>
    internal ChannelReader<AuditEnvelope> SecurityReader => _security.Reader;
}
