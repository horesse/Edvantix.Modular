using EDV.Framework.Mailing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SendGrid;

namespace EDV.Framework.Mailing;

public static class Extensions
{
    public static IServiceCollection AddMailing(this IServiceCollection services)
    {
        services.AddOptions<MailOptions>()
            .BindConfiguration(nameof(MailOptions))
            .ValidateOnStart();

        // Один клиент SendGrid (и его HttpClient) используется во всём процессе —
        // создание клиента на каждую отправку приводит к утечке сокетов при нагрузке.
        // Фабрика ленивая, поэтому клиент создаётся только при реальном использовании SendGrid.
        services.AddSingleton<ISendGridClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MailOptions>>().Value;
            return new SendGridClient(options.SendGrid?.ApiKey ?? string.Empty);
        });

        services.AddTransient<IMailService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MailOptions>>().Value;
            if (options.UseSendGrid)
            {
                return new SendGridMailService(
                    sp.GetRequiredService<IOptions<MailOptions>>(),
                    sp.GetRequiredService<ISendGridClient>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SendGridMailService>>());
            }
            return new SmtpMailService(sp.GetRequiredService<IOptions<MailOptions>>(), sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SmtpMailService>>());
        });
        return services;
    }
}