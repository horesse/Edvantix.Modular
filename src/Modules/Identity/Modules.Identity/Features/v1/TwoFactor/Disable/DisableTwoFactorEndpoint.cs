using EDV.Modules.Identity.Contracts.v1.TwoFactor;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.TwoFactor.Disable;

public static class DisableTwoFactorEndpoint
{
    internal static RouteHandlerBuilder MapDisableTwoFactorEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/2fa/disable",
                async (DisableTwoFactorCommand command, IMediator mediator, CancellationToken ct) =>
                    TypedResults.Ok(new { success = await mediator.Send(command, ct) }))
            .WithName("DisableTwoFactor")
            .WithSummary("Отключить TOTP для текущего пользователя")
            .WithDescription("Отключает 2FA после подтверждения текущего пароля. Также обновляет секрет аутентификатора, чтобы повторная регистрация начиналась с чистого листа.")
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }
}
