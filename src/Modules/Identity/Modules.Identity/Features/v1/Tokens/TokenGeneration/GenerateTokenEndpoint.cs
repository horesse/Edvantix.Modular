using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.ComponentModel;

namespace EDV.Modules.Identity.Features.v1.Tokens.TokenGeneration;

public static class GenerateTokenEndpoint
{
    /// <summary>
    /// Заголовок, используемый клиентами для указания, какая оболочка приложения запрашивает токен.
    /// Аккаунты SuperAdmin (корневой арендатор) ограничены только admin-приложением — передача
    /// "dashboard" при tenant=root возвращает 403 вместо полезного токена. Это дополнительная
    /// подстраховка; клиент dashboard также локально отклоняет токены корневого арендатора
    /// ради более чистого UX.
    /// </summary>
    public const string AppHeader = "X-App";
    public const string AppAdmin = "admin";
    public const string AppDashboard = "dashboard";

    public static RouteHandlerBuilder MapGenerateTokenEndpoint(this IEndpointRouteBuilder endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return endpoint.MapPost("/token/issue",
            [AllowAnonymous] async Task<Results<Ok<TokenResponse>, UnauthorizedHttpResult, ProblemHttpResult>>
            ([FromBody] GenerateTokenCommand command,
            [DefaultValue("root")][FromHeader] string tenant,
            [FromHeader(Name = AppHeader)] string? app,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
            {
                if (IsRootViaDashboard(tenant, app))
                {
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "Границы приложения",
                        detail: "Аккаунты SuperAdmin должны использовать admin-приложение. Войдите там вместо dashboard арендатора.");
                }

                var token = await mediator.Send(command, ct);
                return token is null
                    ? TypedResults.Unauthorized()
                    : TypedResults.Ok(token);
            })
            .WithName("IssueJwtTokens")
            .WithSummary("Выпустить JWT access и refresh токены")
            .WithDescription("Отправьте учётные данные, чтобы получить JWT access-токен и refresh-токен. Укажите заголовок 'tenant', чтобы выбрать контекст арендатора (по умолчанию 'root'). Заголовок 'X-App' (admin|dashboard) используется для соблюдения границы SuperAdmin / dashboard.")
            .Produces<TokenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static bool IsRootViaDashboard(string tenant, string? app)
    {
        return string.Equals(tenant, MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(app, AppDashboard, StringComparison.OrdinalIgnoreCase);
    }
}
