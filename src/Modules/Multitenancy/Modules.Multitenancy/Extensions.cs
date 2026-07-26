using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Microsoft.AspNetCore.Builder;

namespace EDV.Modules.Multitenancy;

public static class Extensions
{
    public static WebApplication UseMultiTenantDatabases(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseMultiTenant();

        return app;
    }
}