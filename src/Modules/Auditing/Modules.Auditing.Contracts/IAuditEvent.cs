namespace EDV.Modules.Auditing.Contracts;

public interface IAuditEvent
{
    /// <summary>Категория события (EntityChange, Security, Activity, Exception…)</summary>
    AuditEventType EventType { get; }

    /// <summary>Уровень серьёзности (None, Info, Error, …)</summary>
    AuditSeverity Severity { get; }

    /// <summary>Время в UTC, когда событие фактически произошло.</summary>
    DateTime OccurredAtUtc { get; }

    /// <summary>Идентификатор арендатора (необязателен в БД на арендатора; всё ещё полезен для экспортов).</summary>
    string? TenantId { get; }

    /// <summary>Id субъекта/пользователя и отображаемое имя (если доступны).</summary>
    string? UserId { get; }
    string? UserName { get; }

    /// <summary>Идентификаторы корреляции/трассировки для распределённой трассировки.</summary>
    string? TraceId { get; }
    string? SpanId { get; }
    string? CorrelationId { get; }
    string? RequestId { get; }

    /// <summary>Логический источник (модуль/сервис) события.</summary>
    string? Source { get; }

    /// <summary>Компактные битовые теги (например, PiiMasked, Sampled).</summary>
    AuditTag Tags { get; }

    /// <summary>Строго типизированный payload (EntityChange, Security, Activity, Exception и т.д.).</summary>
    object Payload { get; }
}
