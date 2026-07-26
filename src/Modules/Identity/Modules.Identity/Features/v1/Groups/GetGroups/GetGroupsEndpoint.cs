using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Groups.GetGroups;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Groups.GetGroups;

public static class GetGroupsEndpoint
{
    public static RouteHandlerBuilder MapGetGroupsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/groups", async (IMediator mediator, string? search, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new GetGroupsQuery(search), cancellationToken)))
        .WithName("ListGroups")
        .WithSummary("Список всех групп")
        .RequirePermission(IdentityPermissions.Groups.View)
        .WithDescription("Возвращает все группы для текущего арендатора с опциональным фильтром поиска.")
        .Produces<IEnumerable<GroupDto>>(StatusCodes.Status200OK);
    }
}