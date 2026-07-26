using EDV.Modules.Identity.Contracts.v1.Users.ChangePassword;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Users.ChangePassword;

public static class ChangePasswordEndpoint
{
    internal static RouteHandlerBuilder MapChangePasswordEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/change-password", async (
            [FromBody] ChangePasswordCommand command,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return TypedResults.Ok(result);
        })
        .WithName("ChangePassword")
        .WithSummary("Сменить пароль")
        .WithDescription("Меняет пароль текущего пользователя.")
        .RequireAuthorization()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status400BadRequest);
    }
}