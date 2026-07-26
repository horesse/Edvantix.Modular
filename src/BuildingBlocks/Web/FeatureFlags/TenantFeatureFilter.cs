using EDV.Framework.Shared.Multitenancy;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.FeatureManagement;

namespace EDV.Framework.Web.FeatureFlags;

/// <summary>
/// Фильтр функций, который включает/отключает функции на основе текущего арендатора.
/// Настройте в appsettings.json с допустимыми идентификаторами арендаторов.
/// </summary>
[FilterAlias("Tenant")]
public sealed class TenantFeatureFilter(
    IHttpContextAccessor httpContextAccessor,
    IMultiTenantContextAccessor<AppTenantInfo>? tenantContextAccessor = null) : IFeatureFilter
{
    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tenantId = tenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            // Откат к заголовку, если контекст арендатора ещё не разрешён
            tenantId = httpContextAccessor.HttpContext?.Request.Headers[MultitenancyConstants.Identifier].ToString();
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Task.FromResult(false);
        }

        var allowedTenants = context.Parameters.GetSection("AllowedTenants").Get<string[]>() ?? [];
        var result = allowedTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(result);
    }
}