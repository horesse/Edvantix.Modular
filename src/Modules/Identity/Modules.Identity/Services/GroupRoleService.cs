using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Identity.Services;

public sealed class GroupRoleService : IGroupRoleService
{
    private readonly IdentityDbContext _dbContext;

    public GroupRoleService(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<string>> GetUserGroupRolesAsync(string userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // Получаем id всех групп, в которых состоит пользователь
        var userGroupIds = await _dbContext.UserGroups
            .Where(ug => ug.UserId == userId)
            .Select(ug => ug.GroupId)
            .ToListAsync(ct);

        if (userGroupIds.Count == 0)
        {
            return [];
        }

        // Получаем все уникальные имена ролей из этих групп
        var groupRoles = await _dbContext.GroupRoles
            .Where(gr => userGroupIds.Contains(gr.GroupId))
            .Select(gr => gr.Role!.Name!)
            .Distinct()
            .ToListAsync(ct);

        return groupRoles;
    }
}