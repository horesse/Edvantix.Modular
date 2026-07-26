using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.TwoFactor;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.TwoFactor.Enroll;

public static class EnrollTwoFactorEndpoint
{
    internal static RouteHandlerBuilder MapEnrollTwoFactorEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/2fa/enroll",
                async (IMediator mediator, CancellationToken ct) =>
                    TypedResults.Ok(await mediator.Send(new EnrollTwoFactorCommand(), ct)))
            .WithName("EnrollTwoFactor")
            .WithSummary("Начать регистрацию TOTP")
            .WithDescription("Генерирует (или обновляет) общий секрет аутентификатора текущего пользователя и возвращает его вместе с URI otpauth:// для отрисовки QR-кода. 2FA НЕ включается, пока вызывающий не подтвердит через /2fa/verify.")
            .RequireAuthorization()
            .Produces<TwoFactorEnrollmentResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
    }
}
