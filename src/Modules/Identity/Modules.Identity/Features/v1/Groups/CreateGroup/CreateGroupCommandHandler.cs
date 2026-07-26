using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Groups.CreateGroup;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Identity.Features.v1.Groups.CreateGroup;

public sealed class CreateGroupCommandHandler : ICommandHandler<CreateGroupCommand, GroupDto>
{
    private readonly IdentityDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public CreateGroupCommandHandler(IdentityDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async ValueTask<GroupDto> Handle(CreateGroupCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Проверяем уникальность имени в пределах арендатора
        var nameExists = await _dbContext.Groups
            .AnyAsync(g => g.Name == command.Name, cancellationToken);

        if (nameExists)
        {
            throw new CustomException($"Группа с именем '{command.Name}' уже существует.", (IEnumerable<string>?)null, System.Net.HttpStatusCode.Conflict);
        }

        // Проверяем, что id ролей существуют — получаем Id+Name одним запросом, чтобы избежать второго обращения позже
        List<(string Id, string Name)> resolvedRoles = [];
        if (command.RoleIds is { Count: > 0 })
        {
            var rawRoles = await _dbContext.Roles
                .Where(r => command.RoleIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Name })
                .ToListAsync(cancellationToken);
            resolvedRoles = rawRoles.Select(r => (r.Id, r.Name!)).ToList();

            var invalidRoleIds = command.RoleIds.Except(resolvedRoles.Select(r => r.Id)).ToList();
            if (invalidRoleIds.Count > 0)
            {
                throw new NotFoundException($"Роли не найдены: {string.Join(", ", invalidRoleIds)}");
            }
        }

        var group = Group.Create(
            name: command.Name,
            description: command.Description,
            isDefault: command.IsDefault,
            isSystemGroup: false,
            createdBy: _currentUser.GetUserId().ToString());

        // Добавляем назначения ролей
        foreach (var role in resolvedRoles)
        {
            _dbContext.GroupRoles.Add(GroupRole.Create(group.Id, role.Item1));
        }

        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new GroupDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            IsDefault = group.IsDefault,
            IsSystemGroup = group.IsSystemGroup,
            MemberCount = 0,
            RoleIds = resolvedRoles.Select(r => r.Id).ToList().AsReadOnly(),
            RoleNames = resolvedRoles.Select(r => r.Name).ToList().AsReadOnly(),
            CreatedAt = group.CreatedOnUtc
        };
    }
}