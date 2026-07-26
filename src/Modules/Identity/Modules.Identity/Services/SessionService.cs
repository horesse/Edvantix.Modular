using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UAParser;

namespace EDV.Modules.Identity.Services;

public sealed class SessionService : ISessionService
{
    private readonly IdentityDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IMultiTenantContextAccessor<AppTenantInfo> _multiTenantContextAccessor;
    private readonly ILogger<SessionService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Parser _uaParser;

    public SessionService(
        IdentityDbContext db,
        ICurrentUser currentUser,
        IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
        ILogger<SessionService> logger,
        TimeProvider timeProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _multiTenantContextAccessor = multiTenantContextAccessor;
        _logger = logger;
        _timeProvider = timeProvider;
        _uaParser = Parser.GetDefault();
    }

    private void EnsureValidTenant()
    {
        if (string.IsNullOrWhiteSpace(_multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id))
        {
            throw new UnauthorizedException("Недействительный арендатор");
        }
    }

    public async Task<UserSessionDto> CreateSessionAsync(
        string userId,
        string refreshTokenHash,
        string ipAddress,
        string userAgent,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var clientInfo = _uaParser.Parse(userAgent);

        var session = UserSession.Create(
            userId: userId,
            refreshTokenHash: refreshTokenHash,
            ipAddress: ipAddress,
            userAgent: userAgent,
            expiresAt: expiresAt,
            deviceType: DeviceTypeClassifier.Classify(clientInfo.Device.Family),
            browser: clientInfo.UA.Family,
            browserVersion: clientInfo.UA.Major,
            operatingSystem: clientInfo.OS.Family,
            osVersion: clientInfo.OS.Major);

        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Создана сессия {SessionId} для пользователя {UserId}", session.Id, userId);
        }

        return MapToDto(session, isCurrentSession: true);
    }

    public async Task<List<UserSessionDto>> GetUserSessionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var currentUserId = _currentUser.GetUserId().ToString();
        if (!string.Equals(userId, currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Нельзя просматривать сессии другого пользователя");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var sessions = await _db.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => MapToDto(s, isCurrentSession: false)).ToList();
    }

    public async Task<List<UserSessionDto>> GetUserSessionsForAdminAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var sessions = await _db.UserSessions
            .AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => MapToDto(s, isCurrentSession: false)).ToList();
    }

    public async Task<(List<UserSessionDto> Items, long TotalCount)> GetTenantSessionsAsync(
        bool includeInactive,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        // Ограничиваем на сервере, чтобы слишком активный клиент не мог вытянуть
        // всю таблицу сессий арендатора за один запрос.
        if (take is < 1 or > 200) take = 50;
        if (skip < 0) skip = 0;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var q = _db.UserSessions
            .AsNoTracking()
            .Include(s => s.User)
            .AsQueryable();

        if (!includeInactive)
        {
            q = q.Where(s => !s.IsRevoked && s.ExpiresAt > now);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(s =>
                (s.User != null && s.User.UserName != null && EF.Functions.ILike(s.User.UserName, $"%{term}%"))
                || (s.User != null && s.User.Email != null && EF.Functions.ILike(s.User.Email, $"%{term}%"))
                || (s.IpAddress != null && EF.Functions.ILike(s.IpAddress, $"%{term}%")));
        }

        long total = await q.LongCountAsync(cancellationToken);

        var sessions = await q
            .OrderByDescending(s => s.LastActivityAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (sessions.Select(s => MapToDto(s, isCurrentSession: false)).ToList(), total);
    }

    public async Task<UserSessionDto?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var session = await _db.UserSessions
            .AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        return session is null ? null : MapToDto(session, isCurrentSession: false);
    }

    public async Task<bool> RevokeSessionAsync(
        Guid sessionId,
        string revokedBy,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var session = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsRevoked, cancellationToken);

        if (session is null)
        {
            return false;
        }

        var currentUserId = _currentUser.GetUserId().ToString();
        if (!string.Equals(session.UserId, currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Нельзя отозвать сессию другого пользователя");
        }

        var tenantId = _multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id;
        session.Revoke(revokedBy, reason ?? "По запросу пользователя", tenantId);

        await _db.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Сессия {SessionId} отозвана пользователем {RevokedBy}", sessionId, revokedBy);
        }

        return true;
    }

    public async Task<int> RevokeAllSessionsAsync(
        string userId,
        string revokedBy,
        Guid? exceptSessionId = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var currentUserId = _currentUser.GetUserId().ToString();
        if (!string.Equals(userId, currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Нельзя отозвать сессии другого пользователя");
        }

        var query = _db.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked);

        if (exceptSessionId.HasValue)
        {
            query = query.Where(s => s.Id != exceptSessionId.Value);
        }

        var sessions = await query.ToListAsync(cancellationToken);

        var tenantId = _multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id;
        foreach (var session in sessions)
        {
            session.Revoke(revokedBy, reason ?? "Пользователь запросил выход со всех устройств", tenantId);
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Отозвано {Count} сессий для пользователя {UserId}", sessions.Count, userId);
        }

        return sessions.Count;
    }

    public async Task<int> RevokeAllSessionsForAdminAsync(
        string userId,
        string revokedBy,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        var tenantId = _multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id;
        foreach (var session in sessions)
        {
            session.Revoke(revokedBy, reason ?? "По запросу администратора", tenantId);
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Администратор {AdminId} отозвал {Count} сессий для пользователя {UserId}",
                revokedBy, sessions.Count, userId);
        }

        return sessions.Count;
    }

    public async Task<bool> RevokeSessionForAdminAsync(
        Guid sessionId,
        string revokedBy,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var session = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsRevoked, cancellationToken);

        if (session is null)
        {
            return false;
        }

        var tenantId = _multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id;
        session.Revoke(revokedBy, reason ?? "Admin requested", tenantId);

        await _db.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Администратор {AdminId} отозвал сессию {SessionId}", revokedBy, sessionId);
        }

        return true;
    }

    public async Task UpdateSessionActivityAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var session = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash && !s.IsRevoked, cancellationToken);

        if (session is not null)
        {
            session.UpdateActivity();
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateSessionRefreshTokenAsync(
        string oldRefreshTokenHash,
        string newRefreshTokenHash,
        DateTime newExpiresAt,
        CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var session = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == oldRefreshTokenHash && !s.IsRevoked, cancellationToken);

        if (session is not null)
        {
            session.UpdateRefreshToken(newRefreshTokenHash, newExpiresAt);
            await _db.SaveChangesAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Сессия {SessionId} обновлена новым refresh-токеном", session.Id);
            }
        }
    }

    public async Task<bool> ValidateSessionAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var session = await _db.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash, cancellationToken);

        if (session is null)
        {
            return true; // Для этого токена отслеживание сессии не ведётся (обратная совместимость)
        }

        return !session.IsRevoked && session.ExpiresAt > _timeProvider.GetUtcNow().UtcDateTime;
    }

    public async Task<Guid?> GetSessionIdByRefreshTokenAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var session = await _db.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash && !s.IsRevoked, cancellationToken);

        return session?.Id;
    }

    public async Task CleanupExpiredSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var cutoffDate = now.AddDays(-30); // Храним отозванные сессии 30 дней для аудита
        var deleted = await _db.UserSessions
            .Where(s => s.ExpiresAt < now && s.ExpiresAt < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Очищено {Count} истёкших сессий", deleted);
        }
    }

    private UserSessionDto MapToDto(UserSession session, bool isCurrentSession)
    {
        return new UserSessionDto
        {
            Id = session.Id,
            UserId = session.UserId,
            UserName = session.User?.UserName,
            UserEmail = session.User?.Email,
            IpAddress = session.IpAddress,
            DeviceType = session.DeviceType,
            Browser = session.Browser,
            BrowserVersion = session.BrowserVersion,
            OperatingSystem = session.OperatingSystem,
            OsVersion = session.OsVersion,
            CreatedAt = session.CreatedAt,
            LastActivityAt = session.LastActivityAt,
            ExpiresAt = session.ExpiresAt,
            IsActive = !session.IsRevoked && session.ExpiresAt > _timeProvider.GetUtcNow().UtcDateTime,
            IsCurrentSession = isCurrentSession
        };
    }
}