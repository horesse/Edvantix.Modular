using EDV.Framework.Quota;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Framework.Tests.Quota;

/// <summary>
/// Защищает DI-связку «квоты включены». <see cref="Extensions.AddQuotas"/> регистрирует сервис квот
/// по типу, а middleware принуждения разрешает его на каждый запрос; хост отключает
/// ValidateOnBuild, поэтому сервис, который контейнер не может сконструировать, падает только на
/// этапе разрешения зависимостей — именно поэтому непубличный конструктор
/// <see cref="InMemoryQuotaService"/> превращал каждый аутентифицированный запрос
/// (включая логин) в 500. Поэтому эти тесты РАЗРЕШАЮТ сервис внутри области видимости
/// (повторяя поведение middleware), а не просто регистрируют его.
/// </summary>
public sealed class QuotaExtensionsTests
{
    private static ServiceProvider BuildProvider(bool enabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QuotaOptions:Enabled"] = enabled ? "true" : "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddQuotas(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddQuotas_Should_ResolveInMemoryService_When_EnabledWithoutRedis()
    {
        // Подготовка
        using var provider = BuildProvider(enabled: true);
        using var scope = provider.CreateScope();

        // Действие — это в точности то разрешение зависимости, которое middleware принуждения выполняет на каждый запрос.
        var quotaService = scope.ServiceProvider.GetRequiredService<IQuotaService>();

        // Проверка
        quotaService.ShouldBeOfType<InMemoryQuotaService>();
    }

    [Fact]
    public void AddQuotas_Should_ResolveNoopService_When_Disabled()
    {
        // Подготовка
        using var provider = BuildProvider(enabled: false);
        using var scope = provider.CreateScope();

        // Действие
        var quotaService = scope.ServiceProvider.GetRequiredService<IQuotaService>();

        // Проверка
        quotaService.ShouldBeOfType<NoopQuotaService>();
    }
}
