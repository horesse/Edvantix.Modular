using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Identity;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Auditing.Contracts;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace EDV.Modules.Identity.Services;

internal sealed class UserStatusService(
    UserManager<AppUser> userManager,
    IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    ICurrentUser currentUser,
    IAuditClient auditClient) : IUserStatusService
{
    // Мягкое удаление функционально идентично деактивации — делегируем, чтобы одни и те же проверки
    // admin/self/last-admin и конвейер аудита применялись единообразно и к DELETE /users/{id}, и к PATCH /users/{id}.
    public Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
        => ToggleStatusAsync(activateUser: false, userId, cancellationToken);

    public async Task ToggleStatusAsync(bool activateUser, string userId, CancellationToken cancellationToken)
    {
        EnsureValidTenant();

        var context = await BuildToggleContextAsync(userId, activateUser, cancellationToken);

        await ValidateTogglePermissionsAsync(context, cancellationToken);

        ApplyStatusChange(context);

        await SaveAndAuditAsync(context, cancellationToken);
    }

    private void EnsureValidTenant()
    {
        if (string.IsNullOrWhiteSpace(multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id))
        {
            throw new UnauthorizedException("недействительный арендатор");
        }
    }

    private async Task<ToggleStatusContext> BuildToggleContextAsync(
        string userId,
        bool activateUser,
        CancellationToken cancellationToken)
    {
        var actorId = currentUser.GetUserId();
        if (actorId == Guid.Empty)
        {
            throw new UnauthorizedException("для изменения статуса требуется аутентифицированный пользователь");
        }

        var actor = await userManager.FindByIdAsync(actorId.ToString())
            ?? throw new UnauthorizedException("текущий пользователь не найден");

        var targetUser = await userManager.Users
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Пользователь не найден.");

        return new ToggleStatusContext(
            ActorId: actorId,
            Actor: actor,
            TargetUser: targetUser,
            ActivateUser: activateUser,
            TenantId: multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id);
    }

    private async Task ValidateTogglePermissionsAsync(
        ToggleStatusContext context,
        CancellationToken cancellationToken)
    {
        if (!await userManager.IsInRoleAsync(context.Actor, RoleConstants.Admin))
        {
            await AuditPolicyFailureAsync(context, "ActorNotAdmin", cancellationToken);
            throw new ForbiddenException("Только администраторы могут изменять статус пользователя.");
        }

        if (!context.ActivateUser && context.ActorId.ToString() == context.TargetUser.Id)
        {
            await AuditPolicyFailureAsync(context, "SelfDeactivationBlocked", cancellationToken);
            throw new CustomException("Пользователи не могут деактивировать сами себя.", Array.Empty<string>(), HttpStatusCode.BadRequest);
        }

        if (!context.ActivateUser && await userManager.IsInRoleAsync(context.TargetUser, RoleConstants.Admin))
        {
            await AuditPolicyFailureAsync(context, "AdminDeactivationBlocked", cancellationToken);
            throw new CustomException("Администраторы не могут быть деактивированы.", Array.Empty<string>(), HttpStatusCode.BadRequest);
        }

        if (!context.ActivateUser)
        {
            await EnsureMinimumActiveAdminsAsync(context, cancellationToken);
        }
    }

    private async Task EnsureMinimumActiveAdminsAsync(
        ToggleStatusContext context,
        CancellationToken cancellationToken)
    {
        var activeAdmins = await userManager.GetUsersInRoleAsync(RoleConstants.Admin);
        if (!activeAdmins.Any(u => u.IsActive))
        {
            await AuditPolicyFailureAsync(context, "NoActiveAdmins", cancellationToken);
            throw new CustomException("В арендаторе должен быть хотя бы один активный администратор.", Array.Empty<string>(), HttpStatusCode.BadRequest);
        }
    }

    private static void ApplyStatusChange(ToggleStatusContext context)
    {
        if (context.ActivateUser)
        {
            context.TargetUser.Activate(context.ActorId.ToString(), context.TenantId);
        }
        else
        {
            context.TargetUser.Deactivate(context.ActorId.ToString(), "Статус изменён администратором", context.TenantId);
        }
    }

    private async Task SaveAndAuditAsync(
        ToggleStatusContext context,
        CancellationToken cancellationToken)
    {
        var result = await userManager.UpdateAsync(context.TargetUser);
        if (!result.Succeeded)
        {
            throw new CustomException("Не удалось изменить статус", result.Errors.Select(e => e.Description).ToList(), HttpStatusCode.BadRequest);
        }

        await auditClient.WriteActivityAsync(
            ActivityKind.Command,
            name: "ToggleUserStatus",
            statusCode: 204,
            durationMs: 0,
            captured: BodyCapture.None,
            requestSize: 0,
            responseSize: 0,
            requestPreview: new { actorId = context.ActorId.ToString(), targetUserId = context.TargetUser.Id, action = context.ActivateUser ? "activate" : "deactivate", tenant = context.TenantId ?? "unknown" },
            responsePreview: new { outcome = "success" },
            severity: AuditSeverity.Information,
            source: "Identity",
            ct: cancellationToken).ConfigureAwait(false);
    }

    private async Task AuditPolicyFailureAsync(
        ToggleStatusContext context,
        string reason,
        CancellationToken cancellationToken)
    {
        var claims = new Dictionary<string, object?>
        {
            ["actorId"] = context.ActorId.ToString(),
            ["targetUserId"] = context.TargetUser.Id,
            ["tenant"] = context.TenantId ?? "unknown",
            ["action"] = context.ActivateUser ? "activate" : "deactivate"
        };

        await auditClient.WriteSecurityAsync(
            SecurityAction.PolicyFailed,
            subjectId: context.ActorId.ToString(),
            reasonCode: reason,
            claims: claims,
            severity: AuditSeverity.Warning,
            source: "Identity",
            ct: cancellationToken).ConfigureAwait(false);
    }

    private sealed record ToggleStatusContext(
        Guid ActorId,
        AppUser Actor,
        AppUser TargetUser,
        bool ActivateUser,
        string? TenantId);
}