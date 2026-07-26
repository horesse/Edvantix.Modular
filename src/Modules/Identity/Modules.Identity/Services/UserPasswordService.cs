using EDV.Framework.Core.Exceptions;
using EDV.Framework.Jobs.Services;
using EDV.Framework.Mailing;
using EDV.Framework.Mailing.Services;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Collections.ObjectModel;
using System.Text;

namespace EDV.Modules.Identity.Services;

internal sealed class UserPasswordService(
    UserManager<AppUser> userManager,
    IdentityDbContext db,
    IJobService jobService,
    IMailService mailService,
    IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    IPasswordHistoryService passwordHistoryService,
    IPasswordExpiryService passwordExpiryService) : IUserPasswordService
{
    public async Task ForgotPasswordAsync(string email, string origin, CancellationToken cancellationToken)
    {
        EnsureValidTenant();

        var user = await userManager.FindByEmailAsync(email);

        // Защита от перечисления: отвечаем одинаково независимо от регистрации — реальный пользователь
        // получает письмо сброса; неизвестный или безымейловый аккаунт молча ничего не делает с тем же 200.
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        // Строим ссылку сброса для SPA через QueryHelpers (аналогично GetEmailVerificationUriAsync): убираем
        // завершающий слэш из настроенного origin (Uri.ToString добавляет его для URL только с хостом →
        // "//reset-password" не совпадает с маршрутом клиента) и включаем арендатора, требуемого страницей сброса.
        // QueryHelpers URL-кодирует каждое значение, так что зарезервированные символы в email (например, '+') сохраняются.
        var resetPasswordUri = QueryHelpers.AddQueryString(
            $"{origin.TrimEnd('/')}/reset-password",
            new Dictionary<string, string?>
            {
                ["token"] = token,
                ["email"] = email,
                ["tenant"] = multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id,
            });
        var mailRequest = new MailRequest(
            new Collection<string> { user.Email },
            "Reset Password",
            $"Please reset your password using the following link: {resetPasswordUri}");

        jobService.Enqueue(() => mailService.SendAsync(mailRequest, CancellationToken.None));
    }

    public async Task ResetPasswordAsync(string email, string password, string token, CancellationToken cancellationToken)
    {
        EnsureValidTenant();

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            throw new NotFoundException("пользователь не найден");
        }

        token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        var result = await userManager.ResetPasswordAsync(user, token, password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            throw new CustomException("ошибка сброса пароля", errors);
        }

        // Фиксируем доменное событие сброса пароля
        var tenantId = multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id;
        user.RecordPasswordChanged(wasReset: true, tenantId);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangePasswordAsync(string password, string newPassword, string confirmNewPassword, string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("пользователь не найден");

        var result = await userManager.ChangePasswordAsync(user, password, newPassword);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            throw new CustomException("не удалось изменить пароль", errors);
        }

        // Фиксируем доменное событие смены пароля
        var tenantId = multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id;
        user.RecordPasswordChanged(wasReset: false, tenantId);
        await db.SaveChangesAsync(cancellationToken);

        // Обновляем дату истечения срока действия пароля
        await passwordExpiryService.UpdateLastPasswordChangeDateAsync(userId, cancellationToken);

        // Сохраняем в историю
        await passwordHistoryService.SavePasswordHistoryAsync(userId, cancellationToken);
    }

    private void EnsureValidTenant()
    {
        if (string.IsNullOrWhiteSpace(multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id))
        {
            throw new UnauthorizedException("недействительный арендатор");
        }
    }
}