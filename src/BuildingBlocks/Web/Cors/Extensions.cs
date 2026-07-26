using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AspNetCorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

namespace EDV.Framework.Web.Cors;

public static class Extensions
{
    private const string PolicyName = "DefaultCorsPolicy";

    public static IServiceCollection AddDefaultCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(nameof(CorsOptions)))
            .Validate(settings => settings.AllowAll || settings.AllowedOrigins.Length > 0, "CorsOptions: AllowedOrigins обязателен, когда AllowAll равен false.")
            .Validate(settings => settings.AllowAll || settings.AllowedHeaders.Length > 0, "CorsOptions: AllowedHeaders обязателен, когда AllowAll равен false.")
            .Validate(settings => settings.AllowAll || settings.AllowedMethods.Length > 0, "CorsOptions: AllowedMethods обязателен, когда AllowAll равен false.")
            .ValidateOnStart();

        services.AddCors();
        services.AddSingleton<IConfigureOptions<AspNetCorsOptions>>(sp =>
        {
            var corsSettings = sp.GetRequiredService<IOptions<CorsOptions>>();
            return new ConfigureOptions<AspNetCorsOptions>(options =>
            {
                options.AddPolicy(PolicyName, builder =>
                {
                    var settings = corsSettings.Value;
                    if (settings.AllowAll)
                    {
                        // Возвращаем источник запроса (не `*`): спецификация CORS запрещает `*` с запросами с учетными данными,
                        // а SignalR negotiate всегда выполняется с учетными данными — поэтому AllowCredentials требует конкретного источника.
                        builder
                            .SetIsOriginAllowed(_ => true)
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    }
                    else
                    {
                        builder
                            .WithOrigins(settings.AllowedOrigins)
                            .WithHeaders(settings.AllowedHeaders)
                            .WithMethods(settings.AllowedMethods)
                            .AllowCredentials();
                    }
                });
            });
        });

        return services;
    }

    public static void UseDefaultCors(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseCors(PolicyName);
    }
}