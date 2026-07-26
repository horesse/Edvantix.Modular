using EDV.Framework.Shared.Identity.Authorization;
using EDV.Framework.Shared.Identity.Claims;
using EDV.Modules.Identity.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace EDV.Modules.Identity.Authorization;

public sealed class RequiredPermissionAuthorizationHandler(IUserService userService) : AuthorizationHandler<PermissionAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionAuthorizationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var httpContext = context.Resource as HttpContext;
        var endpoint = context.Resource switch
        {
            HttpContext ctx => ctx.GetEndpoint(),
            Endpoint ep => ep,
            _ => null,
        };

        // ВАЖНО: разрешаем IRequiredPermissionMetadata из EDV.Framework.Shared.Identity.Authorization
        // (интерфейс, который реализует атрибут) — дубликат мог бы молча пропускать каждый .RequirePermission().
        var requiredPermissions = endpoint?.Metadata.GetMetadata<IRequiredPermissionMetadata>()?.RequiredPermissions;
        if (requiredPermissions == null)
        {
            // Для конечной точки не установлены требования к разрешениям
            // следовательно, авторизуем запросы
            context.Succeed(requirement);
            return;
        }

        var cancellationToken = httpContext?.RequestAborted ?? CancellationToken.None;
        if (context.User?.GetUserId() is { } userId && await userService.HasPermissionAsync(userId, requiredPermissions.First(), cancellationToken).ConfigureAwait(false))
        {
            context.Succeed(requirement);
        }
    }
}