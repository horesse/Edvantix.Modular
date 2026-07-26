using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Groups.DeleteGroup;
using EDV.Modules.Identity.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Identity.Features.v1.Groups.DeleteGroup;

public sealed class DeleteGroupCommandHandler : ICommandHandler<DeleteGroupCommand, Unit>
{
    private readonly IdentityDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IUserPermissionService _userPermissionService;

    public DeleteGroupCommandHandler(IdentityDbContext dbContext, ICurrentUser currentUser, IUserPermissionService userPermissionService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _userPermissionService = userPermissionService;
    }

    public async ValueTask<Unit> Handle(DeleteGroupCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await _dbContext.Groups
            .FirstOrDefaultAsync(g => g.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException($"Группа с ID '{command.Id}' не найдена.");

        if (group.IsSystemGroup)
        {
            throw new ForbiddenException("Системные группы нельзя удалить.");
        }

        // Снимаем список участников до удаления; мягкое удаление переключает IsDeleted, но строки
        // членства сохраняются, поэтому фиксируем их заранее для ясности.
        var memberIds = await _dbContext.UserGroups
            .Where(ug => ug.GroupId == command.Id)
            .Select(ug => ug.UserId)
            .ToListAsync(cancellationToken);

        // Мягкое удаление через доменный метод
        group.Delete(_currentUser.GetUserId().ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Удалённая группа больше не может вносить свои роли в эффективный набор разрешений
        // участников — сбрасываем закэшированную запись каждого из них.
        foreach (var userId in memberIds)
        {
            await _userPermissionService.InvalidatePermissionCacheAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }
}