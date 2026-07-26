using EDV.Framework.Core.Context;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Sessions.AdminRevokeSession;
using Mediator;

namespace EDV.Modules.Identity.Features.v1.Sessions.AdminRevokeSession;

public sealed class AdminRevokeSessionCommandHandler : ICommandHandler<AdminRevokeSessionCommand, bool>
{
    private readonly ISessionService _sessionService;
    private readonly ICurrentUser _currentUser;

    public AdminRevokeSessionCommandHandler(ISessionService sessionService, ICurrentUser currentUser)
    {
        _sessionService = sessionService;
        _currentUser = currentUser;
    }

    public async ValueTask<bool> Handle(AdminRevokeSessionCommand command, CancellationToken cancellationToken)
    {
        var adminId = _currentUser.GetUserId().ToString();

        // Получаем сессию, чтобы убедиться, что она принадлежит указанному пользователю
        var session = await _sessionService.GetSessionAsync(command.SessionId, cancellationToken);
        if (session is null || session.UserId != command.UserId.ToString())
        {
            return false;
        }

        // Используем административный метод отзыва (не проверяет владение)
        return await _sessionService.RevokeSessionForAdminAsync(
            command.SessionId,
            adminId,
            command.Reason ?? "Отозвано администратором",
            cancellationToken);
    }
}