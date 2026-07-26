using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Groups.AddUsersToGroup;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Identity.Features.v1.Groups.AddUsersToGroup;

public sealed class AddUsersToGroupCommandHandler : ICommandHandler<AddUsersToGroupCommand, AddUsersToGroupResponse>
{
    private readonly IdentityDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IUserPermissionService _userPermissionService;

    public AddUsersToGroupCommandHandler(IdentityDbContext dbContext, ICurrentUser currentUser, IUserPermissionService userPermissionService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _userPermissionService = userPermissionService;
    }

    public async ValueTask<AddUsersToGroupResponse> Handle(AddUsersToGroupCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Проверяем, что группа существует
        var groupExists = await _dbContext.Groups
            .AnyAsync(g => g.Id == command.GroupId, cancellationToken);

        if (!groupExists)
        {
            throw new NotFoundException($"Группа с ID '{command.GroupId}' не найдена.");
        }

        // Проверяем, что id пользователей существуют
        var existingUserIds = await _dbContext.Users
            .Where(u => command.UserIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var invalidUserIds = command.UserIds.Except(existingUserIds).ToList();
        if (invalidUserIds.Count > 0)
        {
            throw new NotFoundException($"Пользователи не найдены: {string.Join(", ", invalidUserIds)}");
        }

        // Получаем существующие членства
        var existingMemberships = await _dbContext.UserGroups
            .Where(ug => ug.GroupId == command.GroupId && command.UserIds.Contains(ug.UserId))
            .Select(ug => ug.UserId)
            .ToListAsync(cancellationToken);

        var alreadyMemberUserIds = existingMemberships.ToList();
        var usersToAdd = command.UserIds.Except(existingMemberships).ToList();

        // Добавляем новые членства
        var currentUserId = _currentUser.GetUserId().ToString();
        foreach (var userId in usersToAdd)
        {
            _dbContext.UserGroups.Add(UserGroup.Create(userId, command.GroupId, currentUserId));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Вступление в группу может дать новые роли (через GroupRoles), питающие claims JWT; инвалидируем
        // закэшированный набор разрешений каждого добавленного пользователя, чтобы следующий запрос отразил изменения.
        foreach (var userId in usersToAdd)
        {
            await _userPermissionService.InvalidatePermissionCacheAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        return new AddUsersToGroupResponse(usersToAdd.Count, alreadyMemberUserIds);
    }
}