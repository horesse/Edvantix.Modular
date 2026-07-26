using EDV.Framework.Mailing;
using EDV.Framework.Mailing.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Framework.Tests.Mailing;

public sealed class MailingExtensionsTests
{
    private static ServiceProvider BuildProvider(bool useSendGrid)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MailOptions:UseSendGrid"] = useSendGrid ? "true" : "false",
                ["MailOptions:From"] = "noreply@x.com",
                ["MailOptions:Smtp:Host"] = "localhost",
                ["MailOptions:Smtp:Port"] = "587",
                ["MailOptions:SendGrid:ApiKey"] = "sg-key",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddMailing();
        return services.BuildServiceProvider();
    }

    #region Выбор сервиса

    [Fact]
    public void AddMailing_Should_RegisterSmtpService_When_UseSendGridFalse()
    {
        // Подготовка
        using var provider = BuildProvider(useSendGrid: false);

        // Действие
        var mailService = provider.GetRequiredService<IMailService>();

        // Проверка
        mailService.ShouldBeOfType<SmtpMailService>();
    }

    [Fact]
    public void AddMailing_Should_RegisterSendGridService_When_UseSendGridTrue()
    {
        // Подготовка
        using var provider = BuildProvider(useSendGrid: true);

        // Действие
        var mailService = provider.GetRequiredService<IMailService>();

        // Проверка
        mailService.ShouldBeOfType<SendGridMailService>();
    }

    [Fact]
    public void AddMailing_Should_ReturnSameServiceCollection_When_Chained()
    {
        // Подготовка
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        // Действие
        var result = services.AddMailing();

        // Проверка
        result.ShouldBeSameAs(services);
    }

    #endregion
}
