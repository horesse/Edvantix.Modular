using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.v1.Users.ResendConfirmationEmail;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Users.ResendConfirmationEmail;

public static class ResendConfirmationEmailEndpoint
{
    internal static RouteHandlerBuilder MapResendConfirmationEmailEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/users/{id:guid}/resend-confirmation-email", Handler)
        .WithName("ResendConfirmationEmail")
        .WithSummary("Повторно отправить подтверждение email пользователя (admin)")
        .RequirePermission(IdentityPermissions.Users.ConfirmEmail)
        .WithDescription("Повторно отправляет ссылку подтверждения email пользователю, который ещё не подтвердил свой адрес.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<NoContent> Handler(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        // Строим базовый URL ссылки подтверждения из запроса, так же как эндпоинт регистрации.
        var origin = $"{context.Request.Scheme}://{context.Request.Host.Value}{context.Request.PathBase.Value}";
        await mediator.Send(new ResendConfirmationEmailCommand(id.ToString(), origin), cancellationToken);
        return TypedResults.NoContent();
    }
}
