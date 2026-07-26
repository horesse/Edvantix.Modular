using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.v1.Impersonation;
using EDV.Modules.Identity.Contracts.v1.Impersonation.GetImpersonationGrants;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Impersonation.GetImpersonationGrants;

public static class GetImpersonationGrantsEndpoint
{
    internal static RouteHandlerBuilder MapGetImpersonationGrantsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/impersonation/grants",
            async ([AsParameters] GetImpersonationGrantsQuery query,
                   IMediator mediator,
                   CancellationToken ct) =>
                TypedResults.Ok(await mediator.Send(query, ct)))
            .WithName("GetImpersonationGrants")
            .WithSummary("Список грантов имперсонализации")
            .WithDescription("Выводит сессии имперсонализации в рамках видимости вызывающего. Администраторы арендатора ограничены грантами, нацеленными на их собственный арендатор; корневые операторы могут фильтровать по любому арендатору.")
            .RequirePermission(IdentityPermissions.Impersonation.View)
            .Produces<IReadOnlyList<ImpersonationGrantDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}
