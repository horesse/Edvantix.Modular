using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Identity;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;

namespace EDV.Modules.Identity.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<IdentityService> _logger;
    private readonly IMultiTenantContextAccessor<AppTenantInfo>? _multiTenantContextAccessor;
    private readonly IGroupRoleService _groupRoleService;
    private readonly TimeProvider _timeProvider;
    private readonly IdentityDbContext _dbContext;
    private readonly int _gracePeriodDays;

    public IdentityService(
        UserManager<AppUser> userManager,
        IMultiTenantContextAccessor<AppTenantInfo>? multiTenantContextAccessor,
        ILogger<IdentityService> logger,
        IGroupRoleService groupRoleService,
        TimeProvider timeProvider,
        IdentityDbContext dbContext,
        IOptions<TenantGraceOptions> graceOptions)
    {
        ArgumentNullException.ThrowIfNull(graceOptions);
        _userManager = userManager;
        _multiTenantContextAccessor = multiTenantContextAccessor;
        _logger = logger;
        _groupRoleService = groupRoleService;
        _timeProvider = timeProvider;
        _dbContext = dbContext;
        _gracePeriodDays = graceOptions.Value.GracePeriodDays;
    }

    public async Task<(string Subject, IEnumerable<Claim> Claims)?>
        ValidateCredentialsAsync(string email, string password, string? twoFactorCode = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);

        var tenant = GetValidatedTenant();
        var user = await FindAndValidateUserByCredentialsAsync(email, password);

        ValidateUserStatus(user);
        ValidateTenantStatus(tenant);

        if (user.TwoFactorEnabled)
        {
            await VerifyTwoFactorOrThrowAsync(user, twoFactorCode);
        }

        var claims = await BuildUserClaimsAsync(user, tenant.Id, ct);
        return (user.Id, claims);
    }

    private async Task VerifyTwoFactorOrThrowAsync(AppUser user, string? twoFactorCode)
    {
        if (string.IsNullOrWhiteSpace(twoFactorCode))
        {
            throw new CustomException(
                "two_factor_required: Для завершения входа требуется код аутентификатора.",
                errors: null,
                HttpStatusCode.Unauthorized);
        }

        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            twoFactorCode);

        if (!valid)
        {
            _logger.LogWarning("Неверный код двухфакторной аутентификации для пользователя {UserId}", user.Id);
            throw new UnauthorizedException("two_factor_invalid: Код аутентификатора недействителен или истёк.");
        }
    }

    public async Task<(string Subject, IEnumerable<Claim> Claims)?>
        ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var tenant = GetValidatedTenant();
        var user = await FindUserByRefreshTokenAsync(refreshToken, tenant.Id, ct);

        ValidateRefreshTokenExpiry(user);
        ValidateUserStatus(user);
        ValidateTenantStatus(tenant);

        var claims = await BuildUserClaimsAsync(user, tenant.Id, ct);
        return (user.Id, claims);
    }

    public async Task StoreRefreshTokenAsync(string subject, string refreshToken, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        // Точечный UPDATE обходит отслеживание изменений + перехватчики Finbuckle (которые дают NRE при межарендаторном IgnoreQueryFilters).
        // Безопасно: id пользователей — глобально уникальные GUID, поэтому ровно одна строка соответствует Id == subject независимо от арендатора.
        var hashedToken = HashToken(refreshToken);
        var updated = await _dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == subject)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.RefreshToken, hashedToken)
                      .SetProperty(u => u.RefreshTokenExpiryTime, expiresAtUtc),
                ct).ConfigureAwait(false);

        if (updated == 0)
        {
            throw new UnauthorizedException("пользователь не найден");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Сохранён refresh-токен для пользователя {UserId}. Хеш токена: {TokenHash}, истекает: {ExpiresAt}",
                subject, hashedToken[..Math.Min(8, hashedToken.Length)], expiresAtUtc);
        }
    }

    public async Task<(string Subject, IEnumerable<Claim> Claims)?>
        BuildClaimsForUserAsync(string userId, string tenantId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(tenantId);

        // IgnoreQueryFilters обходит фильтр арендатора Finbuckle, чтобы вызывающие код из корневого
        // арендатора могли разрешать пользователей в других арендаторах во время имперсонации.
        var user = await _userManager.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId && EF.Property<string>(u, "TenantId") == tenantId)
            .FirstOrDefaultAsync(ct);

        if (user is null)
        {
            return null;
        }

        ValidateUserStatus(user);

        var claims = CreateBasicClaims(user, tenantId);

        var userRoleIds = await _dbContext.UserRoles
            .IgnoreQueryFilters()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

        if (userRoleIds.Count > 0)
        {
            var roleNames = await _dbContext.Roles
                .IgnoreQueryFilters()
                .Where(r => userRoleIds.Contains(r.Id) && EF.Property<string>(r, "TenantId") == tenantId)
                .Select(r => r.Name!)
                .ToListAsync(ct);

            claims.AddRange(roleNames.Select(r => new Claim(ClaimTypes.Role, r)));
        }

        return (user.Id, claims);
    }

    private AppTenantInfo GetValidatedTenant()
    {
        var tenant = _multiTenantContextAccessor!.MultiTenantContext.TenantInfo
            ?? throw new UnauthorizedException();

        if (string.IsNullOrWhiteSpace(tenant.Id))
        {
            throw new UnauthorizedException();
        }

        return tenant;
    }

    private async Task<AppUser> FindAndValidateUserByCredentialsAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim().Normalize());
        if (user is null)
        {
            // Общий 401 — никогда не подтверждаем и не опровергаем существование аккаунта на этом пути.
            throw new UnauthorizedException();
        }

        // Проверка блокировки выполняется ДО проверки пароля, чтобы злоумышленник не мог отличить
        // заблокированный аккаунт от неверного пароля по каждому запросу.
        if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Попытка входа в заблокированный аккаунт {UserId}", user.Id);
            throw new CustomException(
                "Аккаунт временно заблокирован из-за слишком большого числа неудачных попыток входа. Повторите попытку позже.",
                errors: null,
                HttpStatusCode.Locked);
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            if (_userManager.SupportsUserLockout)
            {
                await _userManager.AccessFailedAsync(user);
                if (await _userManager.IsLockedOutAsync(user))
                {
                    _logger.LogWarning(
                        "Аккаунт {UserId} заблокирован после превышения порога неудачных попыток входа.",
                        user.Id);
                }
            }
            throw new UnauthorizedException();
        }

        // Успешная аутентификация сбрасывает счётчик неудачных попыток.
        if (_userManager.SupportsUserLockout && await _userManager.GetAccessFailedCountAsync(user) > 0)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        return user;
    }

    private async Task<AppUser> FindUserByRefreshTokenAsync(string refreshToken, string tenantId, CancellationToken ct)
    {
        var hashedToken = HashToken(refreshToken);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Проверка refresh-токена для арендатора {TenantId}. Хеш токена: {TokenHash}",
                tenantId, hashedToken[..Math.Min(8, hashedToken.Length)]);
        }

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == hashedToken, ct);

        if (user is null)
        {
            _logger.LogWarning("Не найден пользователь с соответствующим хешем refresh-токена для арендатора {TenantId}", tenantId);
            throw new UnauthorizedException("refresh-токен недействителен или истёк");
        }

        return user;
    }

    private void ValidateRefreshTokenExpiry(AppUser user)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (user.RefreshTokenExpiryTime <= now)
        {
            _logger.LogWarning(
                "Refresh-токен истёк для пользователя {UserId}. Истёк: {ExpiryTime}, текущее время: {CurrentTime}",
                user.Id, user.RefreshTokenExpiryTime, now);
            throw new UnauthorizedException("refresh-токен недействителен или истёк");
        }
    }

    private static void ValidateUserStatus(AppUser user)
    {
        if (!user.IsActive)
        {
            throw new UnauthorizedException("пользователь деактивирован");
        }

        if (!user.EmailConfirmed)
        {
            throw new UnauthorizedException("email не подтверждён");
        }
    }

    private void ValidateTenantStatus(AppTenantInfo tenant)
    {
        if (tenant.Id == MultitenancyConstants.Root.Id)
        {
            return;
        }

        if (!tenant.IsActive)
        {
            throw new UnauthorizedException($"арендатор {tenant.Id} деактивирован");
        }

        // Учитываем льготный период биллинга: арендатор с истёкшей подпиской всё ещё может
        // аутентифицироваться до ValidUpto + grace (совпадает с проверкой на уровне запроса в MultitenancyModule).
        if (_timeProvider.GetUtcNow().UtcDateTime > tenant.ValidUpto.AddDays(_gracePeriodDays))
        {
            throw new UnauthorizedException($"срок действия арендатора {tenant.Id} истёк");
        }
    }

    private async Task<List<Claim>> BuildUserClaimsAsync(AppUser user, string tenantId, CancellationToken ct)
    {
        var claims = CreateBasicClaims(user, tenantId);
        await AddRoleClaimsAsync(claims, user, ct);
        return claims;
    }

    private static List<Claim> CreateBasicClaims(AppUser user, string tenantId)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return
        [
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // Краткие claims sub/name/email по RFC 7519 публикуются наряду с устаревшими ClaimTypes.*, чтобы потребители JWT читали их по спецификации.
            // `name` публикуется явно, потому что маппинг исходящих claims по умолчанию превращает ClaimTypes.Name в `unique_name`, а не в `name`.
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, fullName.Length > 0 ? fullName : (user.Email ?? string.Empty)),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.FirstName ?? string.Empty),
            new(ClaimTypes.MobilePhone, user.PhoneNumber ?? string.Empty),
            new(ClaimConstants.Fullname, fullName),
            new(ClaimTypes.Surname, user.LastName ?? string.Empty),
            new(ClaimConstants.Tenant, tenantId),
            new(ClaimConstants.ImageUrl, user.ImageUrl?.ToString() ?? string.Empty)
        ];
    }

    private async Task AddRoleClaimsAsync(List<Claim> claims, AppUser user, CancellationToken ct)
    {
        var directRoles = await _userManager.GetRolesAsync(user);
        var groupRoles = await _groupRoleService.GetUserGroupRolesAsync(user.Id, ct);

        var allRoles = directRoles.Union(groupRoles).Distinct();
        claims.AddRange(allRoles.Select(r => new Claim(ClaimTypes.Role, r)));
    }

    private static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}