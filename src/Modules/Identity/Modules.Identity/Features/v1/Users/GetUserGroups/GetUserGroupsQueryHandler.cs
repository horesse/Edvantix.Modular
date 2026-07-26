using EDV.Framework.Core.Exceptions;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Users.GetUserGroups;
using EDV.Modules.Identity.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Identity.Features.v1.Users.GetUserGroups;

public sealed class GetUserGroupsQueryHandler : IQueryHandler<GetUserGroupsQuery, IEnumerable<GroupDto>>
{
    private readonly IdentityDbContext _dbContext;

    public GetUserGroupsQueryHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async ValueTask<IEnumerable<GroupDto>> Handle(GetUserGroupsQuery query, CancellationToken cancellationToken)
    {
        // Проверяем, что пользователь существует
        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == query.UserId, cancellationToken);

        if (!userExists)
        {
            throw new NotFoundException($"Пользователь с ID '{query.UserId}' не найден.");
        }

        // Получаем группы пользователя
        var groupIds = await _dbContext.UserGroups
            .AsNoTracking()
            .Where(ug => ug.UserId == query.UserId)
            .Select(ug => ug.GroupId)
            .ToListAsync(cancellationToken);

        if (groupIds.Count == 0)
        {
            return [];
        }

        var groups = await _dbContext.Groups
            .AsNoTracking()
            .Include(g => g.GroupRoles)
            .Where(g => groupIds.Contains(g.Id))
            .ToListAsync(cancellationToken);

        // Получаем количество участников
        var memberCounts = await _dbContext.UserGroups
            .AsNoTracking()
            .Where(ug => groupIds.Contains(ug.GroupId))
            .GroupBy(ug => ug.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancellationToken);

        // Получаем имена ролей
        var allRoleIds = groups
            .SelectMany(g => g.GroupRoles.Select(gr => gr.RoleId))
            .Distinct()
            .ToList();

        var roleNames = allRoleIds.Count > 0
            ? await _dbContext.Roles
                .AsNoTracking()
                .Where(r => allRoleIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.Name!, cancellationToken)
            : new Dictionary<string, string>();

        return groups.Select(g => new GroupDto
        {
            Id = g.Id,
            Name = g.Name,
            Description = g.Description,
            IsDefault = g.IsDefault,
            IsSystemGroup = g.IsSystemGroup,
            MemberCount = memberCounts.GetValueOrDefault(g.Id, 0),
            RoleIds = g.GroupRoles.Select(gr => gr.RoleId).ToList().AsReadOnly(),
            RoleNames = g.GroupRoles
                .Select(gr => roleNames.GetValueOrDefault(gr.RoleId, gr.RoleId))
                .ToList()
                .AsReadOnly(),
            CreatedAt = g.CreatedOnUtc
        });
    }
}