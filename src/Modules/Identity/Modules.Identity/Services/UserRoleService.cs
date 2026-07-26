using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Identity;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace EDV.Modules.Identity.Services;

internal sealed class UserRoleService(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    IdentityDbContext db,
    IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    ICurrentUser currentUser,
    IUserPermissionService userPermissionService) : IUserRoleService
{
    public async Task<string> AssignRolesAsync(string userId, List<UserRoleDto> userRoles, CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("пользователь не найден");

        await ValidateAdminRoleChangeAsync(user, userRoles);

        var assignedRoles = await ProcessRoleAssignmentsAsync(user, userRoles);

        await RaiseRolesAssignedEventAsync(user, assignedRoles, cancellationToken);

        // Любое изменение ролей (добавление или удаление) инвалидирует закэшированный набор разрешений.
        // Сброс выполняется безусловно, а не по списку assignedRoles, который отслеживает только добавления.
        await userPermissionService.InvalidatePermissionCacheAsync(userId, cancellationToken).ConfigureAwait(false);

        return "Роли пользователя успешно обновлены.";
    }

    public async Task<List<UserRoleDto>> GetUserRolesAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("пользователь не найден");

        var roles = await roleManager.Roles.AsNoTracking().ToListAsync(cancellationToken)
            ?? throw new NotFoundException("роли не найдены");

        // Один запрос членства вместо одного обращения IsInRoleAsync на каждую роль.
        var memberships = await userManager.GetRolesAsync(user);
        var membershipSet = new HashSet<string>(memberships, StringComparer.OrdinalIgnoreCase);

        var userRoles = new List<UserRoleDto>();
        foreach (var role in roles)
        {
            userRoles.Add(new UserRoleDto
            {
                RoleId = role.Id,
                RoleName = role.Name,
                Description = role.Description,
                Enabled = membershipSet.Contains(role.Name!)
            });
        }

        return userRoles;
    }

    private async Task ValidateAdminRoleChangeAsync(AppUser user, List<UserRoleDto> userRoles)
    {
        bool isRemovingAdminRole = userRoles.Exists(a => !a.Enabled && a.RoleName == RoleConstants.Admin);
        if (!isRemovingAdminRole)
        {
            return;
        }

        bool userIsAdmin = await userManager.IsInRoleAsync(user, RoleConstants.Admin);
        if (!userIsAdmin)
        {
            return;
        }

        // Администраторы не могут разжаловать сами себя — они немедленно потеряли бы доступ
        // на следующем запросе, и им понадобился бы другой администратор для восстановления.
        var actorId = currentUser.GetUserId();
        if (actorId != Guid.Empty && string.Equals(actorId.ToString(), user.Id, StringComparison.Ordinal))
        {
            throw new CustomException(
                "Администраторы не могут снять с себя роль администратора.",
                Array.Empty<string>(),
                HttpStatusCode.BadRequest);
        }

        // Изначальный администратор корневого арендатора — учётная запись восстановления последней инстанции для фреймворка.
        if (IsRootTenantAdmin(user))
        {
            throw new ForbiddenException("Администратор корневого арендатора не может быть разжалован.");
        }

        // После этого удаления в арендаторе должен остаться хотя бы один администратор — соответствует
        // инварианту "минимум один активный администратор", применяемому при деактивации пользователя.
        await EnsureMinimumAdminCountAsync();
    }

    private bool IsRootTenantAdmin(AppUser user)
    {
        return user.Email == MultitenancyConstants.Root.EmailAddress
            && multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id == MultitenancyConstants.Root.Id;
    }

    private async Task EnsureMinimumAdminCountAsync()
    {
        int adminCount = (await userManager.GetUsersInRoleAsync(RoleConstants.Admin)).Count;
        if (adminCount <= 1)
        {
            throw new CustomException(
                "В арендаторе должен остаться хотя бы один администратор.",
                Array.Empty<string>(),
                HttpStatusCode.BadRequest);
        }
    }

    private async Task<List<string>> ProcessRoleAssignmentsAsync(AppUser user, List<UserRoleDto> userRoles)
    {
        var assignedRoles = new List<string>();

        foreach (var userRole in userRoles)
        {
            if (await roleManager.FindByNameAsync(userRole.RoleName!) is null)
            {
                continue;
            }

            if (userRole.Enabled)
            {
                if (!await userManager.IsInRoleAsync(user, userRole.RoleName!))
                {
                    await userManager.AddToRoleAsync(user, userRole.RoleName!);
                    assignedRoles.Add(userRole.RoleName!);
                }
            }
            else
            {
                await userManager.RemoveFromRoleAsync(user, userRole.RoleName!);
            }
        }

        return assignedRoles;
    }

    private async Task RaiseRolesAssignedEventAsync(AppUser user, List<string> assignedRoles, CancellationToken cancellationToken)
    {
        if (assignedRoles.Count == 0)
        {
            return;
        }

        var tenantId = multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id;
        user.RecordRolesAssigned(assignedRoles, tenantId);
        await db.SaveChangesAsync(cancellationToken);
    }
}