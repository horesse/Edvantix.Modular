using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.TwoFactor;
using EDV.Modules.Identity.Domain;
using Mediator;
using Microsoft.AspNetCore.Identity;
using System.Text.Encodings.Web;

namespace EDV.Modules.Identity.Features.v1.TwoFactor.Enroll;

public sealed class EnrollTwoFactorCommandHandler
    : ICommandHandler<EnrollTwoFactorCommand, TwoFactorEnrollmentResponse>
{
    private const string IssuerName = "Edvantix";

    private readonly UserManager<AppUser> _userManager;
    private readonly ICurrentUser _currentUser;

    public EnrollTwoFactorCommandHandler(UserManager<AppUser> userManager, ICurrentUser currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async ValueTask<TwoFactorEnrollmentResponse> Handle(
        EnrollTwoFactorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.IsAuthenticated())
        {
            throw new UnauthorizedException();
        }

        var userId = _currentUser.GetUserId().ToString();
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException($"Пользователь {userId} не найден.");

        // Всегда сбрасываем, чтобы повторный вызов enroll обновлял секрет — это предотвращает
        // тихий успех устаревших кодов от предыдущей незавершённой регистрации.
        await _userManager.ResetAuthenticatorKeyAsync(user);
        var sharedKey = await _userManager.GetAuthenticatorKeyAsync(user)
            ?? throw new CustomException("Не удалось сгенерировать ключ аутентификатора.");

        var email = user.Email ?? user.UserName ?? user.Id;
        var authenticatorUri = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
            UrlEncoder.Default.Encode(IssuerName),
            UrlEncoder.Default.Encode(email),
            sharedKey);

        return new TwoFactorEnrollmentResponse(sharedKey, authenticatorUri);
    }
}
