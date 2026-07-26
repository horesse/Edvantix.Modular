using Microsoft.Extensions.DependencyInjection;

namespace EDV.Framework.Web.Sse;

public static class Extensions
{
    /// <summary>
    /// Регистрирует менеджер SSE-соединений как синглтон.
    /// </summary>
    public static IServiceCollection AddSse(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<SseConnectionManager>();
        services.AddScoped<ISseTokenService, SseTokenService>();
        return services;
    }
}