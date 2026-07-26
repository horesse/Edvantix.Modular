namespace EDV.Modules.Auditing.Contracts;

/// <summary>
/// Окружающий контекст текущей операции/запроса.
/// Реализации обычно берут данные из HttpContext, провайдера арендатора и Activity.Current.
/// </summary>
public interface IAuditScope
{
    string? TenantId { get; }
    string? UserId { get; }
    string? UserName { get; }
    string? TraceId { get; }
    string? SpanId { get; }
    string? CorrelationId { get; }
    string? RequestId { get; }
    string? Source { get; }

    /// <summary>Теги по умолчанию, применяемые ко всем событиям в этой области.</summary>
    AuditTag Tags { get; }

    /// <summary>Клонирует область с дополнительными тегами (неразрушающая операция).</summary>
    IAuditScope WithTags(AuditTag tags);

    /// <summary>Клонирует область, переопределяя выбранные поля (null сохраняет текущее значение).</summary>
    IAuditScope WithProperties(
        string? tenantId = null,
        string? userId = null,
        string? userName = null,
        string? traceId = null,
        string? spanId = null,
        string? correlationId = null,
        string? requestId = null,
        string? source = null,
        AuditTag? tags = null);
}
