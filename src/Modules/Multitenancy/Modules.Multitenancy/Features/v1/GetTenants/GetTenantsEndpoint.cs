using EDV.Framework.Shared.Identity.Authorization;
using EDV.Framework.Shared.Persistence;
using EDV.Modules.Multitenancy.Contracts.Authorization;
using EDV.Modules.Multitenancy.Contracts.Dtos;
using EDV.Modules.Multitenancy.Contracts.v1.GetTenants;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Multitenancy.Features.v1.GetTenants;

public static class GetTenantsEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet(
                "/",
                async ([AsParameters] GetTenantsQuery query, IMediator mediator, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await mediator.Send(query, cancellationToken)))
            .WithName("ListTenants")
            .WithSummary("Получить список тенантов")
            .WithDescription("Возвращает тенантов текущего окружения с постраничной разбивкой и опциональной сортировкой.")
            .RequirePermission(MultitenancyPermissions.Tenants.View)
            .Produces<PagedResponse<TenantDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}