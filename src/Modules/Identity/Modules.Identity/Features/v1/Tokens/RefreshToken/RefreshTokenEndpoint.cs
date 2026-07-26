using EDV.Modules.Identity.Contracts.v1.Tokens.RefreshToken;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Tokens.RefreshToken;

public static class RefreshTokenEndpoint
{
    public static RouteHandlerBuilder MapRefreshTokenEndpoint(this IEndpointRouteBuilder endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return endpoint.MapPost("/token/refresh",
            [AllowAnonymous] async Task<Results<Ok<RefreshTokenCommandResponse>, UnauthorizedHttpResult, ProblemHttpResult>>
            ([FromBody] RefreshTokenCommand command,
            [FromHeader(Name = "tenant")] string tenant,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
            {
                var response = await mediator.Send(command, ct);
                return TypedResults.Ok(response);
            })
            .WithName("RefreshJwtTokens")
            .WithSummary("Обновить JWT access и refresh токены")
            .WithDescription("Используйте действительный (возможно, истёкший) access-токен вместе с действительным refresh-токеном, чтобы получить новый access-токен и повёрнутый refresh-токен.")
            .Produces<RefreshTokenCommandResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }
}