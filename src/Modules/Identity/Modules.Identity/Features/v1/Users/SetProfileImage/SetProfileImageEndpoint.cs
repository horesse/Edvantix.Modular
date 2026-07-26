using EDV.Modules.Identity.Contracts.v1.Users.SetProfileImage;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Users.SetProfileImage;

public static class SetProfileImageEndpoint
{
    internal static RouteHandlerBuilder MapSetProfileImageEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPut("/profile/image",
                async (SetProfileImageCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(command, ct);
                    return Results.NoContent();
                })
            .WithName("SetProfileImage")
            .WithSummary("Установить URL аватара аутентифицированного пользователя")
            .WithDescription("Сохраняет постоянный URL изображения в профиле текущего пользователя. Обычно вызывается после того, как поток предварительно подписанной загрузки модуля Files вернёт publicUrl. Передайте null/пустое тело, чтобы очистить.")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);
}
