using EDV.Framework.Core.Exceptions;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Groups.RemoveUserFromGroup;
using EDV.Modules.Identity.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Identity.Features.v1.Groups.RemoveUserFromGroup;

public sealed class RemoveUserFromGroupCommandHandler : ICommandHandler<RemoveUserFromGroupCommand, Unit>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IUserPermissionService _userPermissionService;

    public RemoveUserFromGroupCommandHandler(IdentityDbContext dbContext, IUserPermissionService userPermissionService)
    {
        _dbContext = dbContext;
        _userPermissionService = userPermissionService;
    }

    public async ValueTask<Unit> Handle(RemoveUserFromGroupCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var membership = await _dbContext.UserGroups
            .Include(ug => ug.Group)
            .FirstOrDefaultAsync(ug => ug.GroupId == command.GroupId && ug.UserId == command.UserId, cancellationToken);

        if (membership is null)
        {
            throw new NotFoundException($"Пользователь '{command.UserId}' не состоит в группе '{command.GroupId}'.");
        }

        // Группы по умолчанию (например, засеянная "All Users") требуют членства каждого пользователя
        // арендатора, поэтому удаление нарушает этот инвариант и оставляет последующих регистрантов
        // в наполовину заполненной группе.
        if (membership.Group is not null && membership.Group.IsDefault)
        {
            throw new ForbiddenException("Пользователей нельзя удалять из группы по умолчанию.");
        }

        _dbContext.UserGroups.Remove(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Выход из группы может отозвать роли, которые пользователь получал только через эту группу —
        // инвалидируем, чтобы закэшированный набор разрешений пересобрался при следующем запросе.
        await _userPermissionService.InvalidatePermissionCacheAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}