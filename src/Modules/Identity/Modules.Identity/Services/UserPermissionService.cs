using EDV.Framework.Caching;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Identity;
using EDV.Modules.Identity.Caching;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace EDV.Modules.Identity.Services;

internal sealed class UserPermissionService(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    IdentityDbContext db,
    HybridCache cache) : IUserPermissionService
{
    // Вынесено наверх во избежание аллокаций на каждый вызов. Небольшой payload (< 4 КБ после base64),
    // поэтому расход CPU на сжатие превышает предельную экономию сети — отключаем его для этого горячего пути.
    private static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(2),
        Flags = HybridCacheEntryFlags.DisableCompression,
    };

    private static readonly string[] Tags = [CacheKeys.Tags.Permissions];

    public async Task<List<string>?> GetPermissionsAsync(string userId, CancellationToken cancellationToken)
    {
        var set = await GetOrLoadAsync(userId, cancellationToken).ConfigureAwait(false);

        // Копируем в новый List<string>, чтобы сохранить публичный контракт; ~50 нс незначительны по сравнению
        // с десериализацией JSON, которую пришлось бы платить на каждое попадание в L1 без оптимизации [ImmutableObject].
        return [.. set.Values];
    }

    public async Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
    {
        // Быстрый путь: используем закэшированный PermissionSet напрямую, чтобы не материализовать
        // List<string> ради проверки одного разрешения. Разделяет запись кэша с GetPermissionsAsync.
        var set = await GetOrLoadAsync(userId, cancellationToken).ConfigureAwait(false);
        return set.Contains(permission);
    }

    public Task InvalidatePermissionCacheAsync(string userId, CancellationToken cancellationToken)
        => cache.RemoveAsync(CacheKeys.UserPermissions(userId), cancellationToken).AsTask();

    private ValueTask<PermissionSet> GetOrLoadAsync(string userId, CancellationToken cancellationToken)
    {
        // Перегрузка со stateless-фабрикой — фабрика является статической группой методов, поэтому
        // среда выполнения переиспользует закэшированный делегат и не выделяет замыкание на каждый вызов (включая попадания в L1).
        var state = new FactoryState(userManager, roleManager, db, userId);

        return cache.GetOrCreateAsync(
            CacheKeys.UserPermissions(userId),
            state,
            LoadPermissionsAsync,
            options: EntryOptions,
            tags: Tags,
            cancellationToken: cancellationToken);
    }

    private static async ValueTask<PermissionSet> LoadPermissionsAsync(FactoryState s, CancellationToken ct)
    {
        var user = await s.UserManager.FindByIdAsync(s.UserId).ConfigureAwait(false);
        _ = user ?? throw new UnauthorizedException();

        var userRoles = await s.UserManager.GetRolesAsync(user).ConfigureAwait(false);

        var directRoleIds = await s.RoleManager.Roles
            .Where(r => userRoles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        // Роли, унаследованные от групп, тоже дают разрешения — JWT уже объединяет их
        // (IdentityService.AddRoleClaimsAsync), и каждое изменение группы инвалидирует эту
        // запись кэша, поэтому эффективный набор должен включать роли, достижимые через UserGroups.
        var groupRoleIds = await s.Db.GroupRoles
            .Where(gr => s.Db.UserGroups
                .Where(ug => ug.UserId == s.UserId)
                .Select(ug => ug.GroupId)
                .Contains(gr.GroupId))
            .Select(gr => gr.RoleId)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        var roleIds = directRoleIds.Union(groupRoleIds, StringComparer.Ordinal).ToList();

        if (roleIds.Count == 0)
        {
            return PermissionSet.Empty;
        }

        // Единственный запрос по всем id ролей — дешевле старого цикла N+1.
        var perms = await s.Db.RoleClaims
            .Where(rc => roleIds.Contains(rc.RoleId) && rc.ClaimType == ClaimConstants.Permission)
            .Select(rc => rc.ClaimValue!)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        return perms.Count == 0
            ? PermissionSet.Empty
            : new PermissionSet([.. perms]);
    }

    // Состояние-структура передаётся через параметр TState HybridCache — избегает аллокации замыкания.
    private readonly record struct FactoryState(
        UserManager<AppUser> UserManager,
        RoleManager<AppRole> RoleManager,
        IdentityDbContext Db,
        string UserId);
}
