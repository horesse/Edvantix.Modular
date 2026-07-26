using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Multitenancy.Contracts.Authorization;
using EDV.Modules.Multitenancy.Contracts.Dtos;
using EDV.Modules.Multitenancy.Contracts.v1.GetTenantStatus;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Multitenancy.Features.v1.GetTenantStatus;

public static class GetTenantStatusEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/{id}/status", async (string id, IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new GetTenantStatusQuery(id), cancellationToken)))
            .WithName("GetTenantStatus")
            .WithSummary("Получить статус тенанта")
            .WithDescription("Возвращает информацию о статусе тенанта, включая активацию, срок действия и базовые метаданные.")
            .RequirePermission(MultitenancyPermissions.Tenants.View)
            .Produces<TenantStatusDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}