using Microsoft.AspNetCore.Builder;

namespace EDV.Framework.Web.Security;

public static class SecurityExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}