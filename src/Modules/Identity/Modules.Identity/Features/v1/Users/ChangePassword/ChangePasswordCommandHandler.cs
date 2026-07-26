using EDV.Framework.Core.Context;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Users.ChangePassword;
using Mediator;

namespace EDV.Modules.Identity.Features.v1.Users.ChangePassword;

public sealed class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand, string>
{
    private readonly IUserService _userService;
    private readonly ICurrentUser _currentUser;

    public ChangePasswordCommandHandler(IUserService userService, ICurrentUser currentUser)
    {
        _userService = userService;
        _currentUser = currentUser;
    }

    public async ValueTask<string> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.IsAuthenticated())
        {
            throw new InvalidOperationException("Пользователь не аутентифицирован.");
        }

        var userId = _currentUser.GetUserId().ToString();

        await _userService.ChangePasswordAsync(command.Password, command.NewPassword, command.ConfirmNewPassword, userId, cancellationToken).ConfigureAwait(false);

        return "письмо для сброса пароля отправлено";
    }
}