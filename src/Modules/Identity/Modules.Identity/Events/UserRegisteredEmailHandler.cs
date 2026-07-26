using EDV.Framework.Eventing.Abstractions;
using EDV.Framework.Mailing;
using EDV.Framework.Mailing.Services;
using EDV.Modules.Identity.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Identity.Events;

/// <summary>
/// Отправляет приветственное письмо при регистрации нового пользователя.
/// </summary>
public sealed class UserRegisteredEmailHandler
    : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly IMailService _mailService;
    private readonly ILogger<UserRegisteredEmailHandler> _logger;

    public UserRegisteredEmailHandler(
        IMailService mailService,
        ILogger<UserRegisteredEmailHandler> logger)
    {
        _mailService = mailService;
        _logger = logger;
    }

    public async Task HandleAsync(UserRegisteredIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (string.IsNullOrWhiteSpace(@event.Email))
        {
            return;
        }

        try
        {
            var mail = new MailRequest(
                to: new System.Collections.ObjectModel.Collection<string> { @event.Email },
                subject: "Welcome!",
                body: $"Hi {@event.FirstName}, thanks for registering.");

            await _mailService.SendAsync(mail, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Сбои отправки письма не должны ломать регистрацию пользователя.
            // Письмо можно повторить через механизм outbox/dead-letter.
            // Минимизация PII: идентифицируем получателя по UserId, а не по адресу электронной почты.
            _logger.LogWarning(ex, "Не удалось отправить приветственное письмо пользователю {UserId}", @event.UserId);
        }
    }
}