using EDV.Modules.Auditing.Contracts;

namespace EDV.Modules.Auditing.Core;

/// <summary>
/// Неизменяемая, минимальная реализация области. Создавайте на каждый запрос/операцию.
/// </summary>
public sealed record DefaultAuditScope(
    string? TenantId,
    string? UserId,
    string? UserName,
    string? TraceId,
    string? SpanId,
    string? CorrelationId,
    string? RequestId,
    string? Source,
    AuditTag Tags
) : IAuditScope
{
    public IAuditScope WithTags(AuditTag tags) => this with { Tags = Tags | tags };

    public IAuditScope WithProperties(
        string? tenantId = null,
        string? userId = null,
        string? userName = null,
        string? traceId = null,
        string? spanId = null,
        string? correlationId = null,
        string? requestId = null,
        string? source = null,
        AuditTag? tags = null)
        => this with
        {
            TenantId = tenantId ?? TenantId,
            UserId = userId ?? UserId,
            UserName = userName ?? UserName,
            TraceId = traceId ?? TraceId,
            SpanId = spanId ?? SpanId,
            CorrelationId = correlationId ?? CorrelationId,
            RequestId = requestId ?? RequestId,
            Source = source ?? Source,
            Tags = tags ?? Tags
        };
}