using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Multitenancy.Contracts.Authorization;
using EDV.Modules.Multitenancy.Contracts.Dtos;
using EDV.Modules.Multitenancy.Contracts.v1.GetTenantTheme;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Multitenancy.Features.v1.GetTenantTheme;

public static class GetTenantThemeEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/theme", async (IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new GetTenantThemeQuery(), cancellationToken)))
            .WithName("GetTenantTheme")
            .WithSummary("Получить тему текущего тенанта")
            .WithDescription("Возвращает настройки темы для текущего тенанта, включая цвета, типографику и брендовые ресурсы.")
            .RequirePermission(MultitenancyPermissions.Tenants.ViewTheme)
            .Produces<TenantThemeDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}