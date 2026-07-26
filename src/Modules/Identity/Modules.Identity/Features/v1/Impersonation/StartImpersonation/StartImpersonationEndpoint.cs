using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.v1.Impersonation;
using EDV.Modules.Identity.Contracts.v1.Impersonation.StartImpersonation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Impersonation.StartImpersonation;

public static class StartImpersonationEndpoint
{
    internal static RouteHandlerBuilder MapStartImpersonationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/impersonation/start",
            async Task<Results<Ok<ImpersonationResponse>, ProblemHttpResult>>
            ([FromBody] StartImpersonationCommand command,
             [FromServices] IMediator mediator,
             CancellationToken ct) =>
            {
                var response = await mediator.Send(command, ct);
                return TypedResults.Ok(response);
            })
            .WithName("StartImpersonation")
            .WithSummary("Начать имперсонализацию пользователя")
            .WithDescription("Выпускает кратковременный access-токен, представляющий целевого пользователя. Токен несёт claims актора (act_sub, act_tenant), идентифицирующие исходного вызывающего. Операторы платформы (корневой арендатор) могут имперсонализировать любого пользователя; администраторы арендатора — только пользователей своего арендатора. Refresh-токен не выпускается.")
            .RequirePermission(IdentityPermissions.Users.Impersonate)
            .Produces<ImpersonationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);
    }
}
