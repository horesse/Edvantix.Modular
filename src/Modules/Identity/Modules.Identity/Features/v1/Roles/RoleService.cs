using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Identity;
using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Shared.Persistence;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace EDV.Modules.Identity.Features.v1.Roles;

public sealed class RoleService(RoleManager<AppRole> roleManager,
    IdentityDbContext context,
    IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    ICurrentUser currentUser,
    IUserPermissionService userPermissionService) : IRoleService
{
    // Инвалидируем каждого пользователя, чьи эффективные разрешения могли измениться из-за мутации роли:
    // прямых держателей (AspNetUserRoles) и держателей через группу (участники групп с этой ролью).
    private async Task InvalidateAffectedUsersAsync(string roleId, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        if (role?.Name is null)
        {
            return;
        }

        // Прямые держатели роли через join AspNetUserRoles.
        var directUserIds = await context.UserRoles
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        // Держатели роли через группу.
        var groupUserIds = await context.GroupRoles
            .Where(gr => gr.RoleId == roleId)
            .SelectMany(gr => context.UserGroups
                .Where(ug => ug.GroupId == gr.GroupId)
                .Select(ug => ug.UserId))
            .ToListAsync(cancellationToken);

        foreach (var userId in directUserIds.Concat(groupUserIds).Distinct())
        {
            await userPermissionService.InvalidatePermissionCacheAsync(userId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<PagedResponse<RoleDto>> GetRolesAsync(
        int pageNumber = 1,
        int pageSize = 20,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (roleManager is null)
            throw new NotFoundException("RoleManager<AppRole> не разрешён. Проверьте регистрацию Identity.");

        if (roleManager.Roles is null)
            throw new NotFoundException("Хранилище ролей не настроено. Убедитесь, что подключены .AddRoles<AppRole>() и EF-хранилища.");

        var page = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 200);

        var query = roleManager.Roles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim().ToLowerInvariant();
            query = query.Where(r =>
                (r.Name != null && r.Name.ToLower().Contains(needle))
                || (r.Description != null && r.Description.ToLower().Contains(needle)));
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderBy(r => r.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(r => new RoleDto { Id = r.Id, Name = r.Name!, Description = r.Description })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResponse<RoleDto>
        {
            Items = rows,
            PageNumber = page,
            PageSize = size,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)size),
        };
    }

    public async Task<RoleDto?> GetRoleAsync(string id, CancellationToken cancellationToken = default)
    {
        AppRole? role = await roleManager.FindByIdAsync(id);

        _ = role ?? throw new NotFoundException("роль не найдена");

        return new RoleDto { Id = role.Id, Name = role.Name!, Description = role.Description };
    }

    public async Task<RoleDto> CreateOrUpdateRoleAsync(string roleId, string name, string description, CancellationToken cancellationToken = default)
    {
        AppRole? role = string.IsNullOrEmpty(roleId)
            ? null
            : await roleManager.FindByIdAsync(roleId);

        if (role != null)
        {
            // Системные роли нельзя изменять — ни переименовывать, ни менять описание.
            EnsureNotSystemRole(role.Name, "Системные роли нельзя изменять.");
            // И ни одну пользовательскую роль нельзя переименовать в имя системной роли.
            EnsureNotSystemRole(name, "Нельзя переименовать роль в имя системной роли.");

            role.Name = name;
            role.Description = description;
            await roleManager.UpdateAsync(role);
        }
        else
        {
            // Нельзя создать новую роль с именем системной роли.
            EnsureNotSystemRole(name, "Нельзя создать роль с именем системной роли.");

            role = new AppRole(name, description);
            await roleManager.CreateAsync(role);
        }

        return new RoleDto { Id = role.Id, Name = role.Name!, Description = role.Description };
    }

    public async Task DeleteRoleAsync(string id, CancellationToken cancellationToken = default)
    {
        AppRole? role = await roleManager.FindByIdAsync(id);

        _ = role ?? throw new NotFoundException("роль не найдена");

        EnsureNotSystemRole(role.Name, "Системные роли нельзя удалять.");

        // Снимаем список затронутых пользователей ДО того, как каскад удалит строки сопоставления ролей,
        // иначе после удаления поиск вернёт пустой набор.
        await InvalidateAffectedUsersAsync(id, cancellationToken).ConfigureAwait(false);

        await roleManager.DeleteAsync(role);
    }

    public async Task<RoleDto> GetWithPermissionsAsync(string id, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleAsync(id, cancellationToken);
        _ = role ?? throw new NotFoundException("роль не найдена");

        role.Permissions = await context.RoleClaims
            .AsNoTracking()
            .Where(c => c.RoleId == id && c.ClaimType == ClaimConstants.Permission)
            .Select(c => c.ClaimValue!)
            .ToListAsync(cancellationToken);

        return role;
    }

    public async Task<string> UpdatePermissionsAsync(string roleId, List<string> permissions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var role = await roleManager.FindByIdAsync(roleId)
            ?? throw new NotFoundException("роль не найдена");

        EnsureNotSystemRole(role.Name, "Разрешения системных ролей управляются фреймворком и не могут быть изменены.");
        FilterRootPermissions(permissions);

        var currentClaims = await roleManager.GetClaimsAsync(role);
        await RemoveRevokedPermissionsAsync(role, currentClaims, permissions, cancellationToken);
        await AddNewPermissionsAsync(role, currentClaims, permissions, cancellationToken);

        // Разрешения роли только что изменились — у каждого пользователя, достижимого через эту
        // роль (напрямую или через членство в группе), теперь устаревшая запись кэша.
        await InvalidateAffectedUsersAsync(roleId, cancellationToken).ConfigureAwait(false);

        return "разрешения обновлены";
    }

    private static void EnsureNotSystemRole(string? roleName, string message)
    {
        if (!string.IsNullOrEmpty(roleName) && RoleConstants.IsDefault(roleName))
        {
            throw new CustomException(message, Array.Empty<string>(), HttpStatusCode.BadRequest);
        }
    }

    private void FilterRootPermissions(List<string> permissions)
    {
        if (multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id == MultitenancyConstants.Root.Id)
        {
            // Корневой оператор может управлять правами, доступными только root.
            return;
        }

        // Убираем каждое разрешение, помеченное IsRoot в реестре. (Прежняя проверка префикса "Permissions.Root."
        // была no-op — ни одно root-разрешение не использует этот префикс — позволяя администратору арендатора
        // выдать себе root-права.)
        var rootOnly = PermissionConstants.Root.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        permissions.RemoveAll(rootOnly.Contains);
    }

    private async Task RemoveRevokedPermissionsAsync(AppRole role, IList<System.Security.Claims.Claim> currentClaims, List<string> permissions, CancellationToken cancellationToken = default)
    {
        var claimsToRemove = currentClaims.Where(c => !permissions.Exists(p => p == c.Value));

        foreach (var claim in claimsToRemove)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await roleManager.RemoveClaimAsync(role, claim);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(error => error.Description).ToList();
                throw new CustomException("операция не удалась", errors);
            }
        }
    }

    private async Task AddNewPermissionsAsync(AppRole role, IList<System.Security.Claims.Claim> currentClaims, List<string> permissions, CancellationToken cancellationToken = default)
    {
        var newPermissions = permissions
            .Where(p => !string.IsNullOrEmpty(p) && !currentClaims.Any(c => c.Value == p))
            .ToList();

        foreach (string permission in newPermissions)
        {
            context.RoleClaims.Add(new AppRoleClaim
            {
                RoleId = role.Id,
                ClaimType = ClaimConstants.Permission,
                ClaimValue = permission,
                CreatedBy = currentUser.GetUserId().ToString()
            });
        }

        if (newPermissions.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}