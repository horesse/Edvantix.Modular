using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Multitenancy.Contracts;
using EDV.Modules.Multitenancy.Contracts.Dtos;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Multitenancy.Features.v1.GetMyTenantStatus;

public static class GetMyTenantStatusEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/me/status", async (
                IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
                ITenantService tenantService,
                CancellationToken cancellationToken) =>
            {
                var tenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id;
                if (string.IsNullOrEmpty(tenantId))
                {
                    return Results.Unauthorized();
                }

                var status = await tenantService.GetStatusAsync(tenantId, cancellationToken).ConfigureAwait(false);
                return Results.Ok(status);
            })
            .WithName("GetMyTenantStatus")
            .WithSummary("Получить статус вызывающего тенанта")
            .WithDescription("Возвращает план, срок действия и состояние истечения/льготного периода для аутентифицированного тенанта — используется дашбордом тенанта для отображения информации о плане и предупреждений об истечении срока.")
            .RequireAuthorization()
            .Produces<TenantStatusDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
    }
}
