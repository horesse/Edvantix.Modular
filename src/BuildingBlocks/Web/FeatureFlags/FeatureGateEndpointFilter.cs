using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace EDV.Framework.Web.FeatureFlags;

/// <summary>
/// Фильтр конечной точки, который ограничивает доступ за флагом функции.
/// Возвращает 404 Not Found, когда функция отключена.
/// </summary>
public sealed class FeatureGateEndpointFilter(string featureName) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var featureManager = context.HttpContext.RequestServices.GetRequiredService<IFeatureManager>();
        if (!await featureManager.IsEnabledAsync(featureName).ConfigureAwait(false))
        {
            return TypedResults.NotFound();
        }

        return await next(context).ConfigureAwait(false);
    }
}

public static class FeatureGateExtensions
{
    /// <summary>
    /// Ограничивает конечную точку флагом функции. Возвращает 404, когда функция отключена.
    /// </summary>
    public static RouteHandlerBuilder RequireFeature(this RouteHandlerBuilder builder, string featureName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter(new FeatureGateEndpointFilter(featureName));
    }
}