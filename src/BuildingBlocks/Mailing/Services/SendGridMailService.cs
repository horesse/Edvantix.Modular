using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace EDV.Framework.Mailing.Services;

public sealed class SendGridMailService : IMailService
{
    private readonly MailOptions _settings;
    private readonly ISendGridClient _client;
    private readonly ILogger<SendGridMailService> _logger;

    public SendGridMailService(IOptions<MailOptions> settings, ISendGridClient client, ILogger<SendGridMailService> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);
        _settings = settings.Value;
        _client = client;
        _logger = logger;
    }

    public async Task SendAsync(MailRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateConfiguration();

        if (request.To is null or { Count: 0 })
        {
            throw new InvalidOperationException("Требуется как минимум один получатель.");
        }

        var from = CreateFromAddress(request);
        var msg = MailHelper.CreateSingleEmail(
            from,
            new EmailAddress(request.To[0]),
            request.Subject,
            request.Body,
            request.Body);

        ConfigureRecipients(msg, request);
        AddAttachments(msg, request);

        var response = await _client.SendEmailAsync(msg, ct).ConfigureAwait(false);

        // Клиент создан с HttpErrorAsException=false, поэтому ответ, отличный от 2xx (неверный ключ, отклонённый
        // получатель, ограничение скорости), возвращается как Response вместо исключения. Тихая потеря
        // ошибки заставляет вызывающего полагать, что письмо доставлено, поэтому её никогда нельзя игнорировать.
        if (IsSuccess(response.StatusCode))
        {
            return;
        }

        var status = (int)response.StatusCode;
        var body = response.Body is not null
            ? await response.Body.ReadAsStringAsync(ct).ConfigureAwait(false)
            : string.Empty;

        // Повторяем только те ошибки, которые могут быть успешно исправлены позже — 429 (ограничение скорости) и 5xx
        // (сторона SendGrid). Выброс исключения направляет их через автоматический повтор Hangfire у вызывающего.
        // Постоянная ошибка 4xx (неверный API-ключ, отклонённый получатель) никогда не будет успешной при повторе,
        // поэтому логируем её явно и возвращаем управление вместо выброса исключения — иначе каждое поставленное
        // в очередь письмо будет повторяться ~10 раз и заполнит очередь недоставленных писем попытками, которые не могут быть успешными.
        if (status == 429 || status >= 500)
        {
            throw new InvalidOperationException(
                $"SendGrid временно не смог отправить сообщение со статусом {status}. {body}".TrimEnd());
        }

        _logger.LogError(
            "SendGrid окончательно отклонил сообщение со статусом {StatusCode}. {Body}",
            status,
            body);
    }

    private static bool IsSuccess(System.Net.HttpStatusCode statusCode) =>
        (int)statusCode is >= 200 and < 300;

    private void ValidateConfiguration()
    {
        if (_settings.SendGrid?.ApiKey is null)
        {
            throw new InvalidOperationException("SendGrid ApiKey не настроен.");
        }
    }

    private EmailAddress CreateFromAddress(MailRequest request)
    {
        var email = request.From ?? _settings.SendGrid?.From ?? _settings.From;
        var displayName = request.DisplayName ?? _settings.SendGrid?.DisplayName ?? _settings.DisplayName;
        return new EmailAddress(email, displayName);
    }

    private static void ConfigureRecipients(SendGridMessage msg, MailRequest request)
    {
        if (request.Cc.Count > 0)
        {
            msg.AddCcs(request.Cc.Select(cc => new EmailAddress(cc)).ToList());
        }

        if (request.Bcc.Count > 0)
        {
            msg.AddBccs(request.Bcc.Select(bcc => new EmailAddress(bcc)).ToList());
        }

        if (request.ReplyTo != null)
        {
            msg.ReplyTo = new EmailAddress(request.ReplyTo, request.ReplyToName);
        }
    }

    private static void AddAttachments(SendGridMessage msg, MailRequest request)
    {
        foreach (var att in request.AttachmentData)
        {
            msg.AddAttachment(att.Key, Convert.ToBase64String(att.Value));
        }
    }
}