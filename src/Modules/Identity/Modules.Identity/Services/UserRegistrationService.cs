using EDV.Framework.Core.Common;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Eventing.Outbox;
using EDV.Framework.Jobs.Services;
using EDV.Framework.Mailing;
using EDV.Framework.Mailing.Services;
using EDV.Framework.Shared.Identity;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Identity.Contracts.Events;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace EDV.Modules.Identity.Services;

internal sealed class UserRegistrationService(
    UserManager<AppUser> userManager,
    IdentityDbContext db,
    IJobService jobService,
    IMailService mailService,
    IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    IOutboxStore outboxStore) : IUserRegistrationService
{
    public async Task<string> GetOrCreateFromPrincipalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();
        ArgumentNullException.ThrowIfNull(principal);

        var email = ExtractEmailFromPrincipal(principal);

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return existingUser.Id;
        }

        var user = await CreateUserFromPrincipalAsync(principal, email);
        await AssignDefaultRoleAndGroupsAsync(user, "ExternalAuth", cancellationToken);
        await PublishUserRegisteredAsync(user, "Identity.ExternalAuth", cancellationToken);

        return user.Id;
    }

    public async Task<string> RegisterAsync(
        string firstName,
        string lastName,
        string email,
        string userName,
        string password,
        string confirmPassword,
        string phoneNumber,
        string origin,
        CancellationToken cancellationToken)
    {
        ValidatePasswordMatch(password, confirmPassword);

        var user = await CreateUserWithPasswordAsync(firstName, lastName, email, userName, password, phoneNumber);
        await AssignDefaultRoleAndGroupsAsync(user, "System", cancellationToken);
        await SendConfirmationEmailAsync(user, origin, cancellationToken);
        await PublishUserRegisteredAsync(user, "Identity", cancellationToken);

        return user.Id;
    }

    public async Task<string> ConfirmEmailAsync(string userId, string code, string tenant, CancellationToken cancellationToken)
    {
        EnsureValidTenant();

        var user = await userManager.Users
            .Where(u => u.Id == userId && !u.EmailConfirmed)
            .FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new CustomException("Произошла ошибка при подтверждении E-Mail.");

        code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await userManager.ConfirmEmailAsync(user, code);

        return result.Succeeded
            ? string.Format(CultureInfo.InvariantCulture, "E-Mail {0} подтверждён. Теперь вы можете использовать эндпоинт /api/tokens для получения JWT.", user.Email)
            : throw new CustomException(string.Format(CultureInfo.InvariantCulture, "Произошла ошибка при подтверждении {0}", user.Email));
    }

    public async Task AdminConfirmEmailAsync(string userId, CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var user = await userManager.Users
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Пользователь {userId} не найден.");

        // Идемпотентно: повторное подтверждение — no-op, а не ошибка.
        if (user.EmailConfirmed)
        {
            return;
        }

        user.EmailConfirmed = true;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new CustomException(string.Format(
                CultureInfo.InvariantCulture,
                "Произошла ошибка при подтверждении email для {0}: {1}",
                user.Email,
                string.Join("; ", result.Errors.Select(e => e.Description))));
        }
    }

    public async Task ResendConfirmationEmailAsync(string userId, string origin, CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var user = await userManager.Users
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Пользователь {userId} не найден.");

        if (user.EmailConfirmed)
        {
            throw new CustomException(string.Format(
                CultureInfo.InvariantCulture,
                "Email для {0} уже подтверждён.",
                user.Email));
        }

        await SendConfirmationEmailAsync(user, origin, cancellationToken);
    }

    public async Task<string> ConfirmPhoneNumberAsync(string userId, string code, CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var user = await userManager.Users
            .Where(u => u.Id == userId && !u.PhoneNumberConfirmed)
            .FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new CustomException("Произошла ошибка при подтверждении номера телефона.");

        code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await userManager.ChangePhoneNumberAsync(user, user.PhoneNumber!, code);

        return result.Succeeded
            ? string.Format(CultureInfo.InvariantCulture, "Номер телефона {0} успешно подтверждён.", user.PhoneNumber)
            : throw new CustomException(string.Format(CultureInfo.InvariantCulture, "Произошла ошибка при подтверждении номера телефона {0}", user.PhoneNumber));
    }

    private void EnsureValidTenant()
    {
        if (string.IsNullOrWhiteSpace(multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id))
        {
            throw new UnauthorizedException("недействительный арендатор");
        }
    }

    private static string ExtractEmailFromPrincipal(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email")
            ?? throw new CustomException("Для внешней аутентификации требуется claim Email.");
    }

    private async Task<AppUser> CreateUserFromPrincipalAsync(ClaimsPrincipal principal, string email)
    {
        var (firstName, lastName, userName) = ExtractUserInfoFromPrincipal(principal, email);

        userName = await EnsureUniqueUserNameAsync(userName);

        var user = new AppUser
        {
            Email = email,
            UserName = userName,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = true,
            PhoneNumberConfirmed = false,
            IsActive = true
        };

        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            throw new CustomException(
                "Не удалось создать пользователя из внешнего principal.",
                errors,
                HttpStatusCode.BadRequest);
        }

        return user;
    }

    private static (string firstName, string lastName, string userName) ExtractUserInfoFromPrincipal(
        ClaimsPrincipal principal, string email)
    {
        var firstName = principal.FindFirstValue(ClaimTypes.GivenName)
            ?? principal.FindFirstValue("given_name")
            ?? string.Empty;

        var lastName = principal.FindFirstValue(ClaimTypes.Surname)
            ?? principal.FindFirstValue("family_name")
            ?? string.Empty;

        var userName = principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue("preferred_username")
            ?? email.Split('@')[0];

        return (firstName, lastName, userName);
    }

    private async Task<string> EnsureUniqueUserNameAsync(string userName)
    {
        if (await userManager.FindByNameAsync(userName) is not null)
        {
            return $"{userName}_{Guid.NewGuid():N}"[..20];
        }
        return userName;
    }

    private static void ValidatePasswordMatch(string password, string confirmPassword)
    {
        if (password != confirmPassword)
        {
            throw new CustomException(
                "Пароли не совпадают.",
                errors: null,
                HttpStatusCode.BadRequest);
        }
    }

    private async Task<AppUser> CreateUserWithPasswordAsync(
        string firstName,
        string lastName,
        string email,
        string userName,
        string password,
        string phoneNumber)
    {
        var user = new AppUser
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            UserName = userName,
            PhoneNumber = phoneNumber,
            IsActive = true,
            EmailConfirmed = false,
            PhoneNumberConfirmed = false,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            // Сбои создания в Identity (дублирующийся email/username, политика паролей, …) —
            // это ошибки пользовательского ввода, а не сбои сервера — отдаём их как 400 с конкретными
            // причинами, чтобы вызывающий код видел *почему* регистрация не удалась, а не голый 500.
            var errors = result.Errors.Select(error => error.Description).ToList();
            throw new CustomException(
                "Не удалось зарегистрировать пользователя.",
                errors,
                HttpStatusCode.BadRequest);
        }

        return user;
    }

    private async Task AssignDefaultRoleAndGroupsAsync(
        AppUser user,
        string source,
        CancellationToken cancellationToken = default)
    {
        await userManager.AddToRoleAsync(user, RoleConstants.Basic);

        var defaultGroups = await db.Groups
            .AsNoTracking()
            .Where(g => g.IsDefault && !g.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var group in defaultGroups)
        {
            db.UserGroups.Add(UserGroup.Create(user.Id, group.Id, source));
        }

        if (defaultGroups.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SendConfirmationEmailAsync(AppUser user, string origin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(user.Email))
        {
            return;
        }

        string emailVerificationUri = await GetEmailVerificationUriAsync(user, origin);
        string emailBody = BuildConfirmationEmailHtml(user.FirstName ?? user.UserName ?? "User", emailVerificationUri);

        var mailRequest = new MailRequest(
            new Collection<string> { user.Email },
            "Confirm Your Email Address",
            emailBody);

        jobService.Enqueue("email", () => mailService.SendAsync(mailRequest, cancellationToken));
    }

    private async Task PublishUserRegisteredAsync(
        AppUser user,
        string source,
        CancellationToken cancellationToken = default)
    {
        var tenantId = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id;
        user.RecordRegistered(tenantId);

        await db.SaveChangesAsync(cancellationToken);

        var integrationEvent = new UserRegisteredIntegrationEvent(
            Id: Guid.NewGuid(),
            OccurredOnUtc: TimeProvider.System.GetUtcNow().UtcDateTime,
            TenantId: tenantId,
            CorrelationId: Guid.NewGuid().ToString(),
            Source: source,
            UserId: user.Id,
            Email: user.Email ?? string.Empty,
            FirstName: user.FirstName ?? string.Empty,
            LastName: user.LastName ?? string.Empty);

        await outboxStore.AddAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetEmailVerificationUriAsync(AppUser user, string origin)
    {
        EnsureValidTenant();

        string code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        const string route = "api/v1/identity/confirm-email";
        var endpointUri = new Uri(string.Concat($"{origin}/", route));

        string verificationUri = QueryHelpers.AddQueryString(endpointUri.ToString(), QueryStringKeys.UserId, user.Id);
        verificationUri = QueryHelpers.AddQueryString(verificationUri, QueryStringKeys.Code, code);
        verificationUri = QueryHelpers.AddQueryString(
            verificationUri,
            MultitenancyConstants.Identifier,
            multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id!);

        return verificationUri;
    }

    private static string BuildConfirmationEmailHtml(string userName, string confirmationUrl)
    {
        return $"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Confirm Your Email</title>
            </head>
            <body style="margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f8fafc;">
                <table role="presentation" style="width: 100%; border-collapse: collapse;">
                    <tr>
                        <td align="center" style="padding: 40px 0;">
                            <table role="presentation" style="width: 100%; max-width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);">
                                <tr>
                                    <td style="padding: 40px 40px 30px 40px; text-align: center; background-color: #2563eb; border-radius: 8px 8px 0 0;">
                                        <h1 style="margin: 0; color: #ffffff; font-size: 24px; font-weight: 600;">Confirm Your Email Address</h1>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding: 40px;">
                                        <p style="margin: 0 0 20px 0; color: #334155; font-size: 16px; line-height: 1.6;">
                                            Hi {WebUtility.HtmlEncode(userName)},
                                        </p>
                                        <p style="margin: 0 0 20px 0; color: #334155; font-size: 16px; line-height: 1.6;">
                                            Thank you for registering! Please confirm your email address by clicking the button below:
                                        </p>
                                        <table role="presentation" style="width: 100%; border-collapse: collapse;">
                                            <tr>
                                                <td align="center" style="padding: 30px 0;">
                                                    <a href="{WebUtility.HtmlEncode(confirmationUrl)}" style="display: inline-block; padding: 14px 32px; background-color: #2563eb; color: #ffffff; text-decoration: none; font-size: 16px; font-weight: 600; border-radius: 6px;">
                                                        Confirm Email Address
                                                    </a>
                                                </td>
                                            </tr>
                                        </table>
                                        <p style="margin: 0 0 20px 0; color: #64748b; font-size: 14px; line-height: 1.6;">
                                            If the button doesn't work, copy and paste this link into your browser:
                                        </p>
                                        <p style="margin: 0 0 20px 0; color: #2563eb; font-size: 14px; line-height: 1.6; word-break: break-all;">
                                            {WebUtility.HtmlEncode(confirmationUrl)}
                                        </p>
                                        <p style="margin: 30px 0 0 0; color: #64748b; font-size: 14px; line-height: 1.6;">
                                            If you didn't create an account, you can safely ignore this email.
                                        </p>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding: 20px 40px; background-color: #f1f5f9; border-radius: 0 0 8px 8px; text-align: center;">
                                        <p style="margin: 0; color: #94a3b8; font-size: 12px;">
                                            This is an automated message. Please do not reply to this email.
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;
    }
}