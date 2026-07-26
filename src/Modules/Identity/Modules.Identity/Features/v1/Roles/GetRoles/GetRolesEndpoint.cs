using EDV.Framework.Shared.Identity.Authorization;
using EDV.Framework.Shared.Persistence;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Roles.GetRoles;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Roles.GetRoles;

public static class GetRolesEndpoint
{
    public static RouteHandlerBuilder MapGetRolesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/roles",
            async ([AsParameters] GetRolesQuery query, IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(query, cancellationToken)))
        .WithName("ListRoles")
        .WithSummary("Список ролей (с пагинацией)")
        .RequirePermission(IdentityPermissions.Roles.View)
        .Produces<PagedResponse<RoleDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .WithDescription("Возвращает роли, доступные для текущего арендатора. Поддерживает пагинацию через PageNumber/PageSize; фильтрацию через Search (регистронезависимая подстрока по имени + описанию).");
    }
}
