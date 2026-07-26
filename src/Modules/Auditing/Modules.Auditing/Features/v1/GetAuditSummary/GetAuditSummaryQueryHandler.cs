using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Modules.Auditing.Contracts;
using EDV.Modules.Auditing.Contracts.Authorization;
using EDV.Modules.Auditing.Contracts.Dtos;
using EDV.Modules.Auditing.Contracts.v1.GetAuditSummary;
using EDV.Modules.Auditing.Persistence;
using EDV.Modules.Identity.Contracts.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Auditing.Features.v1.GetAuditSummary;

public sealed class GetAuditSummaryQueryHandler : IQueryHandler<GetAuditSummaryQuery, AuditSummaryAggregateDto>
{
    public static readonly TimeSpan MaxWindow = TimeSpan.FromDays(90);
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(7);

    private readonly AuditDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IUserPermissionService _permissions;
    private readonly TimeProvider _timeProvider;

    public GetAuditSummaryQueryHandler(
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

    public async ValueTask<AuditSummaryAggregateDto> Handle(GetAuditSummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (fromUtc, toUtc) = ResolveWindow(query.FromUtc, query.ToUtc);
        var baseQuery = await BuildBaseQueryAsync(query, cancellationToken).ConfigureAwait(false);

        var scoped = baseQuery.Where(a => a.OccurredAtUtc >= fromUtc && a.OccurredAtUtc <= toUtc);

        // Четыре GROUP BY выполняются в SQL по одному и тому же отфильтрованному набору (без материализации).
        // Последовательно, потому что они разделяют один DbContext; для параллельности нужны четыре контекста.
        var byType = await scoped
            .GroupBy(a => a.EventType)
            .Select(g => new { Key = g.Key, Count = (long)g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var bySeverity = await scoped
            .GroupBy(a => a.Severity)
            .Select(g => new { Key = g.Key, Count = (long)g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var bySource = await scoped
            .Where(a => a.Source != null)
            .GroupBy(a => a.Source!)
            .Select(g => new { Key = g.Key, Count = (long)g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byTenant = await scoped
            .Where(a => a.TenantId != null)
            .GroupBy(a => a.TenantId!)
            .Select(g => new { Key = g.Key, Count = (long)g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AuditSummaryAggregateDto
        {
            EventsByType = byType.ToDictionary(x => (AuditEventType)x.Key, x => x.Count),
            EventsBySeverity = bySeverity.ToDictionary(x => (AuditSeverity)x.Key, x => x.Count),
            EventsBySource = bySource.ToDictionary(x => x.Key, x => x.Count, StringComparer.OrdinalIgnoreCase),
            EventsByTenant = byTenant.ToDictionary(x => x.Key, x => x.Count, StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// Зеркалирует <c>GetAuditsQueryHandler.BuildBaseQueryAsync</c>: по умолчанию
    /// ограничен текущим арендатором, межарендаторный доступ — по явному
    /// разрешению ViewCrossTenant.
    /// </summary>
    private async Task<IQueryable<AuditRecord>> BuildBaseQueryAsync(GetAuditSummaryQuery query, CancellationToken ct)
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
            throw new ForbiddenException("Межарендаторная сводка аудита требует Permissions.AuditTrails.ViewCrossTenant.");
        }

        return _dbContext.AuditRecords
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == requested);
    }

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
