using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Multitenancy.Contracts.Authorization;
using EDV.Modules.Multitenancy.Contracts.Dtos;
using EDV.Modules.Multitenancy.Contracts.v1.UpdateTenantTheme;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Multitenancy.Features.v1.UpdateTenantTheme;

public static class UpdateTenantThemeEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/theme", async (TenantThemeDto theme, IMediator mediator, CancellationToken cancellationToken) =>
            {
                await mediator.Send(new UpdateTenantThemeCommand(theme), cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("UpdateTenantTheme")
            .WithSummary("Обновить тему текущего тенанта")
            .WithDescription("Обновляет настройки темы для текущего тенанта, включая цвета, типографику и компоновку.")
            .RequirePermission(MultitenancyPermissions.Tenants.UpdateTheme)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}