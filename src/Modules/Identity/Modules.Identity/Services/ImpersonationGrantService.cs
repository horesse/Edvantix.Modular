using EDV.Framework.Caching;
using EDV.Framework.Core.Exceptions;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Impersonation;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace EDV.Modules.Identity.Services;

internal sealed class ImpersonationGrantService(
    IdentityDbContext db,
    HybridCache cache,
    TimeProvider timeProvider) : IImpersonationGrantService
{
    public async Task<ImpersonationGrantDto> CreateAsync(CreateGrantInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var grant = ImpersonationGrant.Create(
            id: Guid.NewGuid(),
            jti: input.Jti,
            actorUserId: input.ActorUserId,
            actorUserName: input.ActorUserName,
            actorTenantId: input.ActorTenantId,
            impersonatedUserId: input.ImpersonatedUserId,
            impersonatedUserName: input.ImpersonatedUserName,
            impersonatedTenantId: input.ImpersonatedTenantId,
            reason: input.Reason,
            startedAtUtc: input.StartedAtUtc,
            expiresAtUtc: input.ExpiresAtUtc,
            clientId: input.ClientId,
            ipAddress: input.IpAddress,
            userAgent: input.UserAgent);

        db.ImpersonationGrants.Add(grant);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Прогреваем кэш статусом Active, чтобы хук JWT срабатывал уже на первом запросе без обращения к БД.
        // Запись самовытесняется по истечении срока жизни токена; промах после истечения безобиден, так как
        // хук трактует истёкшие токены как завершённые.
        await SetCachedStatusAsync(grant, GrantState.Active, ct).ConfigureAwait(false);

        return ToDto(grant);
    }

    public async Task<ImpersonationGrantDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var grant = await db.ImpersonationGrants
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, ct)
            .ConfigureAwait(false);
        return grant is null ? null : ToDto(grant);
    }

    public async Task<ImpersonationGrantDto?> MarkEndedByJtiAsync(string jti, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(jti);

        var grant = await db.ImpersonationGrants
            .FirstOrDefaultAsync(g => g.Jti == jti, ct)
            .ConfigureAwait(false);
        if (grant is null) return null;
        if (grant.IsTerminal) return ToDto(grant);

        grant.MarkEnded(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await SetCachedStatusAsync(grant, GrantState.EndedOrRevoked, ct).ConfigureAwait(false);

        return ToDto(grant);
    }

    public async Task<ImpersonationGrantDto> RevokeAsync(
        Guid id,
        string revokedByUserId,
        string? revokedByUserName,
        string? reason,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(revokedByUserId);

        var grant = await db.ImpersonationGrants
            .FirstOrDefaultAsync(g => g.Id == id, ct)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("грант имперсонализации не найден");

        if (grant.IsTerminal)
        {
            // Идемпотентно — возвращаем вызывающему коду уже существующее терминальное состояние.
            return ToDto(grant);
        }

        grant.Revoke(
            revokedAtUtc: timeProvider.GetUtcNow().UtcDateTime,
            revokedByUserId: revokedByUserId,
            revokedByUserName: revokedByUserName,
            reason: reason);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        // Записываем маркер отзыва ДО возврата, чтобы конкурирующий запрос
        // не проскочил с закэшированным маркером Active.
        await SetCachedStatusAsync(grant, GrantState.EndedOrRevoked, ct).ConfigureAwait(false);

        return ToDto(grant);
    }

    public async Task<bool> IsRevokedOrEndedAsync(string jti, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(jti)) return false;

        var state = await cache.GetOrCreateAsync(
            CacheKeys.ImpersonationGrantStatus(jti),
            new FactoryState(db, jti),
            LoadStateAsync,
            options: CacheEntryOptions,
            cancellationToken: ct).ConfigureAwait(false);

        return state == GrantState.EndedOrRevoked || state == GrantState.Unknown;
    }

    public async Task<IReadOnlyList<ImpersonationGrantDto>> ListAsync(
        ImpersonationGrantStatus? status,
        string? impersonatedTenantId,
        string? actorUserId,
        int take,
        CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        IQueryable<ImpersonationGrant> q = db.ImpersonationGrants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(impersonatedTenantId))
        {
            q = q.Where(g => g.ImpersonatedTenantId == impersonatedTenantId);
        }
        if (!string.IsNullOrWhiteSpace(actorUserId))
        {
            q = q.Where(g => g.ActorUserId == actorUserId);
        }
        if (status is { } s)
        {
            q = s switch
            {
                ImpersonationGrantStatus.Active =>
                    q.Where(g => !g.RevokedAtUtc.HasValue && !g.EndedAtUtc.HasValue && g.ExpiresAtUtc > now),
                ImpersonationGrantStatus.Ended =>
                    q.Where(g => g.EndedAtUtc.HasValue),
                ImpersonationGrantStatus.Revoked =>
                    q.Where(g => g.RevokedAtUtc.HasValue),
                ImpersonationGrantStatus.Expired =>
                    q.Where(g => !g.RevokedAtUtc.HasValue && !g.EndedAtUtc.HasValue && g.ExpiresAtUtc <= now),
                _ => q,
            };
        }

        var rows = await q
            .OrderByDescending(g => g.StartedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct).ConfigureAwait(false);

        return rows.Select(ToDto).ToList();
    }

    // ── вспомогательные методы ────────────────────────────────────────────

    private static readonly HybridCacheEntryOptions CacheEntryOptions = new()
    {
        // TTL в 5 минут достаточен: сессии живут максимум 60 минут, а запись перепрогревается
        // при каждом revoke/end. После истечения jti всё равно бессмысленен (JwtBearer отклоняет по сроку жизни).
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1),
        Flags = HybridCacheEntryFlags.DisableCompression,
    };

    private Task SetCachedStatusAsync(ImpersonationGrant grant, GrantState state, CancellationToken ct)
        => cache.SetAsync(
            CacheKeys.ImpersonationGrantStatus(grant.Jti),
            state,
            options: CacheEntryOptions,
            cancellationToken: ct).AsTask();

    private static async ValueTask<GrantState> LoadStateAsync(FactoryState s, CancellationToken ct)
    {
        var row = await s.Db.ImpersonationGrants
            .AsNoTracking()
            .Where(g => g.Jti == s.Jti)
            .Select(g => new { g.EndedAtUtc, g.RevokedAtUtc })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        if (row is null) return GrantState.Unknown;
        if (row.EndedAtUtc.HasValue || row.RevokedAtUtc.HasValue) return GrantState.EndedOrRevoked;
        return GrantState.Active;
    }

    private readonly record struct FactoryState(IdentityDbContext Db, string Jti);

    /// <summary>
    /// Удобное для кэша трёхзначное состояние. Хранение enum (а не всего гранта целиком)
    /// делает запись в кэше крошечной и избегает проблем с устаревшими данными — вся
    /// информация из строки, реально нужная на горячем пути, сворачивается до "этот
    /// грант всё ещё действителен?".
    /// </summary>
    private enum GrantState : byte
    {
        Active = 0,
        EndedOrRevoked = 1,
        /// <summary>Для этого jti не найдена строка — трактуем как отозванный (защитно).</summary>
        Unknown = 2,
    }

    private ImpersonationGrantDto ToDto(ImpersonationGrant g)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        ImpersonationGrantStatus status;
        if (g.RevokedAtUtc.HasValue)
        {
            status = ImpersonationGrantStatus.Revoked;
        }
        else if (g.EndedAtUtc.HasValue)
        {
            status = ImpersonationGrantStatus.Ended;
        }
        else if (g.ExpiresAtUtc <= now)
        {
            status = ImpersonationGrantStatus.Expired;
        }
        else
        {
            status = ImpersonationGrantStatus.Active;
        }

        return new ImpersonationGrantDto(
            Id: g.Id,
            Jti: g.Jti,
            ActorUserId: g.ActorUserId,
            ActorUserName: g.ActorUserName,
            ActorTenantId: g.ActorTenantId,
            ImpersonatedUserId: g.ImpersonatedUserId,
            ImpersonatedUserName: g.ImpersonatedUserName,
            ImpersonatedTenantId: g.ImpersonatedTenantId,
            Reason: g.Reason,
            StartedAtUtc: g.StartedAtUtc,
            ExpiresAtUtc: g.ExpiresAtUtc,
            EndedAtUtc: g.EndedAtUtc,
            RevokedAtUtc: g.RevokedAtUtc,
            RevokedByUserId: g.RevokedByUserId,
            RevokedByUserName: g.RevokedByUserName,
            RevokeReason: g.RevokeReason,
            Status: status);
    }
}
