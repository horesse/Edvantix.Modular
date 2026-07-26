using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Modules.Identity.Contracts.v1.TwoFactor;
using EDV.Modules.Identity.Domain;
using Mediator;
using Microsoft.AspNetCore.Identity;

namespace EDV.Modules.Identity.Features.v1.TwoFactor.Disable;

public sealed class DisableTwoFactorCommandHandler
    : ICommandHandler<DisableTwoFactorCommand, bool>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ICurrentUser _currentUser;

    public DisableTwoFactorCommandHandler(UserManager<AppUser> userManager, ICurrentUser currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async ValueTask<bool> Handle(
        DisableTwoFactorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.IsAuthenticated())
        {
            throw new UnauthorizedException();
        }

        var userId = _currentUser.GetUserId().ToString();
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException($"Пользователь {userId} не найден.");

        // Требуем текущий пароль, чтобы одного украденного access-токена было недостаточно
        // для понижения защиты аккаунта.
        if (!await _userManager.CheckPasswordAsync(user, command.CurrentPassword))
        {
            throw new UnauthorizedException("Текущий пароль неверен.");
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        return true;
    }
}
