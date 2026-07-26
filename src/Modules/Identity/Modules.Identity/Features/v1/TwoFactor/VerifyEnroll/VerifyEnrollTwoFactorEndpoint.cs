using EDV.Modules.Identity.Contracts.v1.TwoFactor;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.TwoFactor.VerifyEnroll;

public static class VerifyEnrollTwoFactorEndpoint
{
    internal static RouteHandlerBuilder MapVerifyEnrollTwoFactorEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/2fa/verify",
                async (VerifyEnrollTwoFactorCommand command, IMediator mediator, CancellationToken ct) =>
                    TypedResults.Ok(new { success = await mediator.Send(command, ct) }))
            .WithName("VerifyEnrollTwoFactor")
            .WithSummary("Подтвердить регистрацию TOTP")
            .WithDescription("Проверяет 6-значный код из приложения-аутентификатора. При успехе 2FA включается, и последующие входы должны включать код.")
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }
}
