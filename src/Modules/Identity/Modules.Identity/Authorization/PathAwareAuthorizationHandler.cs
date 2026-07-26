using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace EDV.Modules.Identity.Authorization;

public class PathAwareAuthorizationHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _fallback = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        var path = context.Request.Path;
        var allowedPaths = new[]
        {
            new PathString("/scalar"),
            new PathString("/openapi"),
            new PathString("/favicon.ico")
        };
        if (allowedPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            // Уважаем маршрутизацию + продолжаем конвейер
            var endpoint = context.GetEndpoint();
            if (endpoint != null)
            {
                await next(context);
                return;
            }

            // Если конечная точка не найдена, явно возвращаем 404
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Конечная точка не найдена.", context.RequestAborted).ConfigureAwait(false);
            return;
        }

        await _fallback.HandleAsync(next, context, policy, authorizeResult);
    }
}