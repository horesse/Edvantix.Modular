using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Identity.Contracts.v1.Users.ResetPassword;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Users.ResetPassword;

public static class ResetPasswordEndpoint
{
    internal static RouteHandlerBuilder MapResetPasswordEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/reset-password",
            async ([FromBody] ResetPasswordCommand command,
            [FromHeader(Name = MultitenancyConstants.Identifier)] string tenant,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return TypedResults.Ok(result);
        })
        .WithName("ResetPassword")
        .WithSummary("Сбросить пароль")
        .WithDescription("Сбрасывает пароль пользователя с использованием предоставленного токена подтверждения.")
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK);
    }
}