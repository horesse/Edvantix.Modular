using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Persistence.Pagination;
using EDV.Framework.Shared.Persistence;
using EDV.Modules.Auditing.Contracts;
using EDV.Modules.Auditing.Contracts.Authorization;
using EDV.Modules.Auditing.Contracts.Dtos;
using EDV.Modules.Auditing.Contracts.v1.GetAudits;
using EDV.Modules.Auditing.Persistence;
using EDV.Modules.Identity.Contracts.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;
using static EDV.Modules.Auditing.Persistence.AuditJsonbFunctions;

namespace EDV.Modules.Auditing.Features.v1.GetAudits;

public sealed class GetAuditsQueryHandler : IQueryHandler<GetAuditsQuery, PagedResponse<AuditSummaryDto>>
{
    /// <summary>
    /// Максимальное окно, допустимое при указании вызывающим from/to. Мы отказываемся
    /// сканировать всю таблицу — без этой защиты неограниченный запрос вырождается
    /// в полное последовательное сканирование по мере роста объёма аудита.
    /// </summary>
    public static readonly TimeSpan MaxWindow = TimeSpan.FromDays(90);

    /// <summary>
    /// Период по умолчанию, когда вызывающий не указал from/to. Держит типовой
    /// запрос для дашборда в разумных границах.
    /// </summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(7);

    private readonly AuditDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IUserPermissionService _permissions;
    private readonly TimeProvider _timeProvider;

    public GetAuditsQueryHandler(
        AuditDbContext dbContext,
        ICurrentUser currentUser,
        IUserPermissionService permissions,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _permissions = permissions;
        _timeProvider = timeProvider;
    }

    public async ValueTask<PagedResponse<AuditSummaryDto>> Handle(GetAuditsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (fromUtc, toUtc) = ResolveWindow(query.FromUtc, query.ToUtc);

        var audits = await BuildBaseQueryAsync(query, cancellationToken).ConfigureAwait(false);

        audits = audits.Where(a => a.OccurredAtUtc >= fromUtc && a.OccurredAtUtc <= toUtc);

        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            audits = audits.Where(a => a.UserId == query.UserId);
        }

        if (query.EventType.HasValue)
        {
            audits = audits.Where(a => a.EventType == (int)query.EventType.Value);
        }

        if (query.ExcludeEventType.HasValue)
        {
            audits = audits.Where(a => a.EventType != (int)query.ExcludeEventType.Value);
        }

        if (query.Severity.HasValue)
        {
            audits = audits.Where(a => a.Severity == (byte)query.Severity.Value);
        }

        if (query.Tags.HasValue && query.Tags.Value != AuditTag.None)
        {
            long tagMask = (long)query.Tags.Value;
            audits = audits.Where(a => (a.Tags & tagMask) != 0);
        }

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            audits = audits.Where(a => a.Source == query.Source);
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            audits = audits.Where(a => a.CorrelationId == query.CorrelationId);
        }

        if (!string.IsNullOrWhiteSpace(query.TraceId))
        {
            audits = audits.Where(a => a.TraceId == query.TraceId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search;
            // ILIKE по PayloadJson — последовательный; индекс (TenantId, OccurredAtUtc)
            // ограничивает сканирование — добавьте GIN-индекс на PayloadJson в проде для быстрого поиска.
            audits = audits.Where(a =>
                (a.PayloadJson != null && EF.Functions.ILike(AsText(a.PayloadJson), $"%{term}%")) ||
                (a.Source != null && EF.Functions.ILike(a.Source, $"%{term}%")) ||
                (a.UserName != null && EF.Functions.ILike(a.UserName, $"%{term}%")));
        }

        audits = audits.OrderByDescending(a => a.OccurredAtUtc);

        IQueryable<AuditSummaryDto> projected = audits.Select(a => new AuditSummaryDto
        {
            Id = a.Id,
            OccurredAtUtc = a.OccurredAtUtc,
            EventType = (AuditEventType)a.EventType,
            Severity = (AuditSeverity)a.Severity,
            TenantId = a.TenantId,
            UserId = a.UserId,
            UserName = a.UserName,
            TraceId = a.TraceId,
            CorrelationId = a.CorrelationId,
            RequestId = a.RequestId,
            Source = a.Source,
            Tags = (AuditTag)a.Tags
        });

        return await projected.ToPagedResponseAsync(query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Возвращает queryable, уже ограниченный нужным арендатором. Если вызывающий
    /// указал TenantId, равный своему собственному, это no-op. Межарендаторный
    /// доступ требует явного разрешения ViewCrossTenant, обходит анонимный фильтр
    /// арендатора Finbuckle, а затем повторно применяет явный предикат TenantId,
    /// чтобы случайно не вернуть строки *всех* арендаторов.
    /// </summary>
    private async Task<IQueryable<AuditRecord>> BuildBaseQueryAsync(GetAuditsQuery query, CancellationToken ct)
    {
        var currentTenant = _currentUser.GetTenant();
        var requested = string.IsNullOrWhiteSpace(query.TenantId) ? null : query.TenantId;

        bool wantsCrossTenant =
            requested is not null
            && !string.Equals(requested, currentTenant, StringComparison.OrdinalIgnoreCase);

        if (!wantsCrossTenant)
        {
            return _dbContext.AuditRecords.AsNoTracking();
        }

        var userId = _currentUser.GetUserId().ToString();
        var allowed = await _permissions
            .HasPermissionAsync(userId, AuditingPermissions.AuditTrails.ViewCrossTenant, ct)
            .ConfigureAwait(false);
        if (!allowed)
        {
            throw new ForbiddenException("Межарендаторный доступ к аудиту требует Permissions.AuditTrails.ViewCrossTenant.");
        }

        return _dbContext.AuditRecords
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == requested);
    }

    /// <summary>
    /// Ограничивает переданное окно значением <see cref="MaxWindow"/> и подставляет
    /// <see cref="DefaultWindow"/>, когда обе границы отсутствуют. Валидатор ловит
    /// очевидные ошибки использования (from &gt; to); этот метод обрабатывает
    /// открытый случай "без диапазона", чтобы SQL всегда был ограничен.
    /// </summary>
    private (DateTime FromUtc, DateTime ToUtc) ResolveWindow(DateTime? from, DateTime? to)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var resolvedTo = to ?? now;
        var resolvedFrom = from ?? resolvedTo - DefaultWindow;

        if (resolvedTo - resolvedFrom > MaxWindow)
        {
            resolvedFrom = resolvedTo - MaxWindow;
        }

        return (resolvedFrom, resolvedTo);
    }
}
