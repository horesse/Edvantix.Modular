using EDV.Framework.Shared.Persistence;
using EDV.Modules.Auditing.Contracts.Dtos;
using Mediator;

namespace EDV.Modules.Auditing.Contracts.v1.GetAudits;

public sealed class GetAuditsQuery : IPagedQuery, IQuery<PagedResponse<AuditSummaryDto>>
{
    public int? PageNumber { get; set; }

    public int? PageSize { get; set; }

    public string? Sort { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public string? TenantId { get; set; }

    public string? UserId { get; set; }

    public AuditEventType? EventType { get; set; }

    /// <summary>Скрыть один тип события (например, <c>Activity</c>, чтобы убрать системный HTTP-шум).
    /// Применяется как фильтр "не равно", чтобы пагинация и итоги оставались корректными.</summary>
    public AuditEventType? ExcludeEventType { get; set; }

    public AuditSeverity? Severity { get; set; }

    public AuditTag? Tags { get; set; }

    public string? Source { get; set; }

    public string? CorrelationId { get; set; }

    public string? TraceId { get; set; }

    public string? Search { get; set; }
}