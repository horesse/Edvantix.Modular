using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace EDV.Framework.Web.OpenApi;

public static class Extensions
{
    /// <summary>
    /// Регистрирует документы OpenAPI для каждой версии API. Каждая версия получает отдельный документ
    /// (например, /openapi/v1.json) с конечными точками, отфильтрованными по этой группе версий.
    /// Чтобы добавить новую версию, добавьте ещё одну запись в массив <c>OpenApiOptions:Versions</c>
    /// или вызовите <c>AddOpenApi("v2", ...)</c> после этого метода.
    /// </summary>
    public static IServiceCollection AddDefaultOpenApi(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<OpenApiOptions>()
            .Bind(configuration.GetSection(nameof(OpenApiOptions)))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Title), "OpenApi:Title обязателен.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Description), "OpenApi:Description обязателен.")
            .ValidateOnStart();

        var edvOptions = configuration.GetSection(nameof(OpenApiOptions)).Get<OpenApiOptions>();

        // Один документ OpenAPI на версию API. GroupNameFormat "'v'VVV" из Asp.Versioning группирует
        // конечные точки как "v1", "v2", …; каждый AddOpenApi(groupName) включает только конечные точки этой группы.
        var versions = edvOptions?.Versions is { Length: > 0 } ? edvOptions.Versions : ["v1"];
        foreach (var version in versions)
        {
            services.AddOpenApi(version, options =>
            {
                options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
                options.AddDocumentTransformer((document, context, _) =>
                {
                    var provider = context.ApplicationServices;
                    var openApi = provider.GetRequiredService<IOptions<OpenApiOptions>>().Value;

                    document.Info = new OpenApiInfo
                    {
                        Title = openApi.Title,
                        Version = version,
                        Description = openApi.Description,
                        Contact = openApi.Contact is null ? null : new OpenApiContact
                        {
                            Name = openApi.Contact.Name,
                            Url = openApi.Contact.Url,
                            Email = openApi.Contact.Email
                        },
                        License = openApi.License is null ? null : new OpenApiLicense
                        {
                            Name = openApi.License.Name,
                            Url = openApi.License.Url
                        }
                    };
                    return Task.CompletedTask;
                });
            });
        }

        return services;
    }

    public static void UseDefaultOpenApi(
        this WebApplication app,
        string openApiPath = "/openapi/{documentName}.json")
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapOpenApi(openApiPath);

        app.MapScalarApiReference(options =>
        {
            var configuration = app.Configuration;
            options
                .WithTitle(configuration["OpenApi:Title"] ?? "Edvantix API")
                .WithTheme(ScalarTheme.Alternate)
                .EnableDarkMode()
                .HideModels()
                .WithOpenApiRoutePattern(openApiPath)
                .AddPreferredSecuritySchemes("Bearer");
        });
    }
}