using EDV.Framework.Shared.Identity.Authorization;
using EDV.Framework.Shared.Persistence;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Sessions.GetTenantSessions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Sessions.GetTenantSessions;

public static class GetTenantSessionsEndpoint
{
    internal static RouteHandlerBuilder MapGetTenantSessionsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/sessions",
                async (
                    bool? includeInactive,
                    string? search,
                    int? pageNumber,
                    int? pageSize,
                    IMediator mediator,
                    CancellationToken ct) =>
                {
                    var query = new GetTenantSessionsQuery
                    {
                        IncludeInactive = includeInactive ?? false,
                        Search = search,
                        PageNumber = pageNumber ?? 1,
                        PageSize = pageSize ?? 50,
                    };
                    return TypedResults.Ok(await mediator.Send(query, ct));
                })
            .WithName("GetTenantSessions")
            .WithSummary("Список всех сессий текущего арендатора (Admin)")
            .WithDescription("Возвращает постраничные сессии по всему арендатору с фильтром по активности и произвольным текстовым поиском по имени пользователя, email и IP-адресу.")
            .RequirePermission(IdentityPermissions.Sessions.ViewAll)
            .Produces<PagedResponse<UserSessionDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}
