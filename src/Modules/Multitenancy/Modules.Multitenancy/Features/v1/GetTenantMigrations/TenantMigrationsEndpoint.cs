using EDV.Framework.Shared.Identity.Authorization;
using EDV.Modules.Multitenancy.Contracts.Authorization;
using EDV.Modules.Multitenancy.Contracts.Dtos;
using EDV.Modules.Multitenancy.Contracts.v1.GetTenantMigrations;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Multitenancy.Features.v1.GetTenantMigrations;

public static class TenantMigrationsEndpoint
{
    public static RouteHandlerBuilder Map(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet(
                "/migrations",
                async (IMediator mediator, CancellationToken cancellationToken) =>
                {
                    IReadOnlyCollection<TenantMigrationStatusDto> result =
                        await mediator.Send(new GetTenantMigrationsQuery(), cancellationToken);

                    return TypedResults.Ok(result);
                })
            .WithName("GetTenantMigrations")
            .RequirePermission(MultitenancyPermissions.Tenants.View)
            .WithSummary("Получить статус миграций по каждому тенанту")
            .WithDescription("Возвращает статус миграций для каждого тенанта, включая ожидающие миграции и информацию о провайдере.")
            .Produces<IReadOnlyCollection<TenantMigrationStatusDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}