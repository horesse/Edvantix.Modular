using EDV.Modules.Auditing.Contracts;
using EDV.Modules.Auditing.Core;
using EDV.Modules.Auditing.Infrastructure.Http;
using EDV.Modules.Auditing.Infrastructure.Serialization;
using EDV.Modules.Auditing.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EDV.Modules.Auditing.Infrastructure.Hosting;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует ядро аудита: публикатор на каналах, фоновый воркер, сериализатор, область и HTTP-настройки.
    /// </summary>
    public static IServiceCollection AddAuditingCore(this IServiceCollection services, IConfiguration config, Action<AuditHttpOptions>? configure = null)
    {
        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAuditClient, DefaultAuditClient>();
        services.AddScoped<ISecurityAudit, SecurityAudit>();
        services.AddDbContext<AuditDbContext>();
        services.AddSingleton<IAuditSerializer, SystemTextJsonAuditSerializer>();

        // Читатель области в рамках запроса (на основе HttpContext)
        services.AddScoped<IAuditScope, HttpAuditScope>();

        // Связка публикатор/sink/воркер: публикатор — singleton и разрешает текущую область из HttpContext.
        services.AddSingleton<ChannelAuditPublisher>();
        services.AddSingleton<IAuditPublisher>(sp => sp.GetRequiredService<ChannelAuditPublisher>());

        services.AddHostedService<AuditBackgroundWorker>();
        services.AddSingleton<IAuditSink, SqlAuditSink>();
        services.AddSingleton<IAuditDlqSink, FileAuditDlqSink>();

        var opts = new AuditHttpOptions();
        configure?.Invoke(opts);
        services.AddSingleton(opts);

        return services;
    }

    /// <summary>
    /// Добавляет middleware HTTP-аудита в конвейер.
    /// Разместите рано (после маршрутизации), но до эндпоинтов.
    /// </summary>
    public static IApplicationBuilder UseAuditHttp(this IApplicationBuilder app)
        => app.UseMiddleware<AuditHttpMiddleware>();
}