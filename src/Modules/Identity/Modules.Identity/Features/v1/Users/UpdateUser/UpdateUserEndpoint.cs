using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Identity.Claims;
using EDV.Modules.Identity.Contracts.v1.Users.UpdateUser;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace EDV.Modules.Identity.Features.v1.Users.UpdateUser;

public static class UpdateUserEndpoint
{
    internal static RouteHandlerBuilder MapUpdateUserEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/profile", async ([FromBody] UpdateUserCommand request, ClaimsPrincipal user, IMediator mediator, CancellationToken cancellationToken) =>
        {
            if (user.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedException();
            }

            // Принудительно устанавливаем целевой id как аутентифицированного пользователя — этот эндпоинт
            // предназначен только для самообновления, независимо от того, какой id передал вызывающий в теле.
            request.Id = userId;

            await mediator.Send(request, cancellationToken);
            return TypedResults.Ok();
        })
        .WithName("UpdateUserProfile")
        .WithSummary("Обновить профиль пользователя")
        .RequireAuthorization()
        .WithDescription("Обновляет данные профиля аутентифицированного пользователя. Любой вошедший пользователь может редактировать свой собственный профиль; права администратора не требуются.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status400BadRequest);
    }
}