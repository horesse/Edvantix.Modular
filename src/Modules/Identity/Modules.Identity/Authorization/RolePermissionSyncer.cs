using EDV.Framework.Caching;
using EDV.Framework.Shared.Identity;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Identity.Authorization;

/// <summary>
/// Добавляет недостающие claims разрешений во встроенные роли (<see cref="RoleConstants.Admin"/>,
/// <see cref="RoleConstants.Basic"/>) для текущего контекста арендатора Finbuckle. Идемпотентно —
/// вставляет только те claims, которых ещё нет, поэтому может безопасно выполняться при каждом запуске.
/// </summary>
public sealed class RolePermissionSyncer(
    IdentityDbContext context,
    RoleManager<AppRole> roleManager,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
    HybridCache cache,
    TimeProvider timeProvider,
    ILogger<RolePermissionSyncer> logger)
{
    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantAccessor.MultiTenantContext.TenantInfo?.Id;
        bool isRoot = tenantId == MultitenancyConstants.Root.Id;

        int basicAdded = await SyncRoleAsync(RoleConstants.Basic, PermissionConstants.Basic, cancellationToken).ConfigureAwait(false);

        // Admin получает все разрешения, не помеченные как Root; Admin корневого арендатора
        // дополнительно получает разрешения Root.
        var adminPermissions = isRoot
            ? PermissionConstants.Admin.Concat(PermissionConstants.Root).Distinct().ToList()
            : PermissionConstants.Admin.ToList();
        int adminAdded = await SyncRoleAsync(RoleConstants.Admin, adminPermissions, cancellationToken).ConfigureAwait(false);

        // Если были записи, сбрасываем кэш разрешений для каждого пользователя, чтобы уже
        // вошедшие сессии видели новые разрешения при следующем запросе, а не ждали TTL.
        if (basicAdded + adminAdded > 0)
        {
            await cache.RemoveByTagAsync(CacheKeys.Tags.Permissions, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<int> SyncRoleAsync(string roleName, IReadOnlyList<AppPermission> targetPermissions, CancellationToken cancellationToken)
    {
        var role = await roleManager.Roles
            .SingleOrDefaultAsync(r => r.Name == roleName, cancellationToken)
            .ConfigureAwait(false);
        if (role is null)
        {
            // Роль ещё не создана — полный IdentityDbInitializer.SeedAsync создаст её при первом запуске.
            return 0;
        }

        var existing = await context.RoleClaims
            .Where(rc => rc.RoleId == role.Id && rc.ClaimType == ClaimConstants.Permission)
            .Select(rc => rc.ClaimValue!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        var toAdd = targetPermissions
            .Where(p => !existingSet.Contains(p.Name))
            .Select(p => new AppRoleClaim
            {
                RoleId = role.Id,
                ClaimType = ClaimConstants.Permission,
                ClaimValue = p.Name,
                CreatedBy = "RolePermissionSyncer",
                CreatedOn = timeProvider.GetUtcNow(),
            })
            .ToList();

        if (toAdd.Count == 0)
        {
            return 0;
        }

        await context.RoleClaims.AddRangeAsync(toAdd, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Синхронизировано {Count} новых claims разрешений для роли '{Role}' в арендаторе '{Tenant}'",
                toAdd.Count,
                roleName,
                tenantAccessor.MultiTenantContext.TenantInfo?.Id);
        }

        return toAdd.Count;
    }
}