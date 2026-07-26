using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Permissions.GetPermissionCatalog;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Identity.Features.v1.Permissions.GetPermissionCatalog;

public static class GetPermissionCatalogEndpoint
{
    internal static RouteHandlerBuilder MapGetPermissionCatalogEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/permissions/catalog", async (IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new GetPermissionCatalogQuery(), cancellationToken)))
        .WithName("GetPermissionCatalog")
        .WithSummary("Получить каталог разрешений")
        .RequirePermission(IdentityPermissions.Roles.View)
        .WithDescription("Возвращает все разрешения, зарегистрированные в хосте, отфильтрованные по контексту арендатора вызывающего. Некорневые арендаторы видят набор Admin; корневой арендатор дополнительно видит набор платформы Root.")
        .Produces<IReadOnlyList<PermissionCatalogEntryDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
