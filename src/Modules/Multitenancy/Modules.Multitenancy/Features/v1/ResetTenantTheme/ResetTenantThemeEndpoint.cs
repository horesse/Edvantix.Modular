using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Multitenancy.Contracts.Authorization;
using EDV.Modules.Multitenancy.Contracts.v1.ResetTenantTheme;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Multitenancy.Features.v1.ResetTenantTheme;

public static class ResetTenantThemeEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/theme/reset", async (IMediator mediator, CancellationToken cancellationToken) =>
            {
                await mediator.Send(new ResetTenantThemeCommand(), cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("ResetTenantTheme")
            .WithSummary("Сбросить тему тенанта к значениям по умолчанию")
            .WithDescription("Сбрасывает настройки темы текущего тенанта к значениям по умолчанию.")
            .RequirePermission(MultitenancyPermissions.Tenants.UpdateTheme)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}