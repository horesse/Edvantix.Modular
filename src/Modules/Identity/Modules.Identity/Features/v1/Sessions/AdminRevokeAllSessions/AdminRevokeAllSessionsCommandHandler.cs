using EDV.Framework.Core.Context;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Sessions.AdminRevokeAllSessions;
using Mediator;

namespace EDV.Modules.Identity.Features.v1.Sessions.AdminRevokeAllSessions;

public sealed class AdminRevokeAllSessionsCommandHandler : ICommandHandler<AdminRevokeAllSessionsCommand, int>
{
    private readonly ISessionService _sessionService;
    private readonly ICurrentUser _currentUser;

    public AdminRevokeAllSessionsCommandHandler(ISessionService sessionService, ICurrentUser currentUser)
    {
        _sessionService = sessionService;
        _currentUser = currentUser;
    }

    public async ValueTask<int> Handle(AdminRevokeAllSessionsCommand command, CancellationToken cancellationToken)
    {
        var adminId = _currentUser.GetUserId().ToString();
        return await _sessionService.RevokeAllSessionsForAdminAsync(
            command.UserId.ToString(),
            adminId,
            command.Reason ?? "Revoked by administrator",
            cancellationToken);
    }
}