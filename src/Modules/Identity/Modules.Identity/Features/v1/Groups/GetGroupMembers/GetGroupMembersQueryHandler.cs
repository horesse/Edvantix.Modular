using EDV.Framework.Core.Exceptions;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Groups.GetGroupMembers;
using EDV.Modules.Identity.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Identity.Features.v1.Groups.GetGroupMembers;

public sealed class GetGroupMembersQueryHandler : IQueryHandler<GetGroupMembersQuery, IEnumerable<GroupMemberDto>>
{
    private readonly IdentityDbContext _dbContext;

    public GetGroupMembersQueryHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async ValueTask<IEnumerable<GroupMemberDto>> Handle(GetGroupMembersQuery query, CancellationToken cancellationToken)
    {
        // Проверяем, что группа существует
        var groupExists = await _dbContext.Groups
            .AsNoTracking()
            .AnyAsync(g => g.Id == query.GroupId, cancellationToken);

        if (!groupExists)
        {
            throw new NotFoundException($"Группа с ID '{query.GroupId}' не найдена.");
        }

        // Получаем членства с информацией о пользователе
        var memberships = await _dbContext.UserGroups
            .AsNoTracking()
            .Where(ug => ug.GroupId == query.GroupId)
            .Join(
                _dbContext.Users,
                ug => ug.UserId,
                u => u.Id,
                (ug, u) => new GroupMemberDto
                {
                    UserId = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    AddedAt = ug.AddedAt,
                    AddedBy = ug.AddedBy
                })
            .OrderBy(m => m.UserName)
            .ToListAsync(cancellationToken);

        return memberships;
    }
}