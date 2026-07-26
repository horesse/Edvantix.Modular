using EDV.Framework.Caching;
using EDV.Framework.Jobs;
using EDV.Framework.Mailing;
using EDV.Framework.Persistence;
using EDV.Framework.Quota;
using EDV.Framework.Shared.Identity;
using EDV.Framework.Web.Auth;
using EDV.Framework.Web.Cors;
using EDV.Framework.Web.Exceptions;
using EDV.Framework.Web.FeatureFlags;
using EDV.Framework.Web.Health;
using EDV.Framework.Web.Idempotency;
using EDV.Framework.Web.Mediator.Behaviors;
using EDV.Framework.Web.Modules;
using EDV.Framework.Web.Observability.Logging.Serilog;
using EDV.Framework.Web.Observability.OpenTelemetry;
using EDV.Framework.Web.OpenApi;
using EDV.Framework.Web.Origin;
using EDV.Framework.Web.RateLimiting;
using EDV.Framework.Web.Realtime;
using EDV.Framework.Web.Security;
using EDV.Framework.Web.Sse;
using EDV.Framework.Web.Versioning;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace EDV.Framework.Web;

public static class Extensions
{
    public static IHostApplicationBuilder AddPlatform(this IHostApplicationBuilder builder, Action<EdvPlatformOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new EdvPlatformOptions();
        configure?.Invoke(options);

        PermissionConstants.Register(SystemPermissions.All);

        builder.Services.AddScoped<CurrentUserMiddleware>();

        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = System.IO.Compression.CompressionLevel.Fastest;
        });

        builder.AddDefaultLogging();
        if (options.EnableOpenTelemetry)
        {
            builder.AddDefaultOpenTelemetry();
        }

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddDatabaseOptions(builder.Configuration);
        builder.Services.AddDefaultRateLimiting(builder.Configuration);

        var corsEnabled = options.EnableCors && IsCorsEnabled(builder.Configuration);
        var openApiEnabled = options.EnableOpenApi && IsOpenApiEnabled(builder.Configuration);

        if (corsEnabled)
        {
            builder.Services.AddDefaultCors(builder.Configuration);
        }

        builder.Services.AddVersioning();

        if (openApiEnabled)
        {
            builder.Services.AddDefaultOpenApi(builder.Configuration);
        }

        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy());

        if (options.EnableJobs)
        {
            builder.Services.AddJobs();
            builder.Services.AddHealthChecks().AddCheck<HangfireHealthCheck>("hangfire");
        }

        if (options.EnableMailing)
        {
            builder.Services.AddMailing();
        }

        if (options.EnableCaching)
        {
            builder.Services.AddCaching(builder.Configuration);
            var cacheConfig = builder.Configuration.GetSection(nameof(CachingOptions)).Get<CachingOptions>();
            if (cacheConfig is not null && !string.IsNullOrEmpty(cacheConfig.Redis))
            {
                builder.Services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis");
            }
        }

        if (options.EnableFeatureFlags)
        {
            builder.Services.AddFeatureFlags(builder.Configuration);
        }

        if (options.EnableIdempotency)
        {
            builder.Services.AddIdempotency(builder.Configuration);
        }

        if (options.EnableSse)
        {
            builder.Services.AddSse();
        }

        if (options.EnableRealtime)
        {
            builder.Services.AddRealtime(builder.Configuration);
        }

        if (options.EnableQuotas)
        {
            builder.Services.AddQuotas(builder.Configuration);
        }

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        builder.Services.AddProblemDetails();
        builder.Services.AddOptions<OriginOptions>().BindConfiguration(nameof(OriginOptions));
        builder.Services.AddOptions<SecurityHeadersOptions>().BindConfiguration(nameof(SecurityHeadersOptions));

        return builder;
    }


    public static WebApplication UsePlatform(this WebApplication app, Action<EdvPipelineOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = new EdvPipelineOptions();
        configure?.Invoke(options);

        var corsEnabled = options.UseCors && IsCorsEnabled(app.Configuration);
        var openApiEnabled = options.UseOpenApi && IsOpenApiEnabled(app.Configuration);

        app.UseExceptionHandler();
        app.UseResponseCompression();

        // CORS ДОЛЖЕН выполняться до UseHttpsRedirection: предварительный OPTIONS не может следовать за HTTP→HTTPS редиректом,
        // иначе браузер заблокирует вызов. Безопасно до маршрутизации, потому что мы используем одну глобальную политику (без [EnableCors]).
        if (corsEnabled)
        {
            app.UseDefaultCors();
        }

        app.UseHttpsRedirection();

        app.UseSecurityHeaders();

        // Обслуживаем статические файлы как можно раньше, чтобы сократить конвейер
        if (options.ServeStaticFiles)
        {
            var assetsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            if (!Directory.Exists(assetsPath))
            {
                Directory.CreateDirectory(assetsPath);
            }

            app.UseStaticFiles();
        }

        app.UseJobDashboard(app.Configuration);
        app.UseRouting();

        if (openApiEnabled)
        {
            app.UseDefaultOpenApi();
        }

        app.UseAuthentication();

        // Позволяем каждому модулю зарегистрировать своё промежуточное ПО (например, Auditing регистрирует AuditHttpMiddleware)
        app.UseModuleMiddlewares();

        app.UseDefaultRateLimiting();

        if (options.UseQuotas)
        {
            app.UseQuotas();
        }

        app.UseAuthorization();

        if (options.MapModules)
        {
            app.MapModules();
        }

        // Всегда открываем конечные точки здоровья
        app.MapDefaultHealthEndpoints();

        if (options.MapSseEndpoints)
        {
            app.MapSseEndpoints();
        }

        if (options.MapRealtime)
        {
            app.MapRealtime();
        }
        app.UseMiddleware<CurrentUserMiddleware>();
        return app;
    }

    private static bool IsCorsEnabled(IConfiguration configuration)
    {
        var allowAll = configuration.GetValue("CorsOptions:AllowAll", false);
        var origins = configuration.GetSection("CorsOptions:AllowedOrigins").Get<string[]>() ?? [];
        return allowAll || origins.Length > 0;
    }

    private static bool IsOpenApiEnabled(IConfiguration configuration)
    {
        return configuration.GetValue("OpenApiOptions:Enabled", true);
    }
}

public sealed class EdvPlatformOptions
{
    public bool EnableCors { get; set; } = true;
    public bool EnableOpenApi { get; set; } = true;
    public bool EnableCaching { get; set; } = false;
    public bool EnableJobs { get; set; } = false;
    public bool EnableMailing { get; set; } = false;
    public bool EnableOpenTelemetry { get; set; } = true;
    public bool EnableFeatureFlags { get; set; } = false;
    public bool EnableIdempotency { get; set; } = true;
    public bool EnableSse { get; set; } = false;
    public bool EnableRealtime { get; set; } = false;
    public bool EnableQuotas { get; set; } = false;
}

public sealed class EdvPipelineOptions
{
    public bool UseCors { get; set; } = true;
    public bool UseOpenApi { get; set; } = true;
    public bool ServeStaticFiles { get; set; } = true;
    public bool MapModules { get; set; } = true;
    public bool MapSseEndpoints { get; set; } = false;
    public bool MapRealtime { get; set; } = false;
    public bool UseQuotas { get; set; } = false;
}