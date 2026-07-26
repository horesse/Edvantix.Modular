using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Impersonation.EndImpersonation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Impersonation.EndImpersonation;

public static class EndImpersonationEndpoint
{
    internal static RouteHandlerBuilder MapEndImpersonationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/impersonation/end",
            [Authorize] async Task<Results<Ok<TokenResponse>, ProblemHttpResult>>
            ([FromServices] IMediator mediator,
             CancellationToken ct) =>
            {
                var token = await mediator.Send(new EndImpersonationCommand(), ct);
                return TypedResults.Ok(token);
            })
            .WithName("EndImpersonation")
            .WithSummary("Завершить имперсонализацию пользователя")
            .WithDescription("Возвращает свежий access + refresh токен для исходного актора на основе claims act_sub/act_tenant, встроенных в токен имперсонализации. Может быть вызван любой аутентифицированной сессией имперсонализации.")
            .Produces<TokenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);
    }
}
