using EDV.Framework.Mailing;
using EDV.Framework.Mailing.Services;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Framework.Tests.Mailing;

// REL-01: SendGridMailService не должен проглатывать не-2xx ответ SendGrid (клиент создаётся с
// HttpErrorAsException=false, поэтому сбой возвращается как Response, а не как исключение). Временные
// сбои (429/5xx) выбрасывают исключение, чтобы вызывающая задача Hangfire повторила попытку; постоянные
// сбои (прочие 4xx) логируются и возвращаются — повтор при неверном ключе / отклонённом получателе
// только переполняет очередь недоставленных сообщений (dead-letter).
public sealed class SendGridMailServiceTests
{
    private static SendGridMailService BuildService(ISendGridClient client)
    {
        var options = Options.Create(new MailOptions
        {
            UseSendGrid = true,
            From = "noreply@x.com",
            SendGrid = new SendGridOptions { ApiKey = "sg-key", From = "noreply@x.com" },
        });
        return new SendGridMailService(options, client, NullLogger<SendGridMailService>.Instance);
    }

    private static ISendGridClient ClientReturning(HttpStatusCode status)
    {
        var client = Substitute.For<ISendGridClient>();
        client.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
            .Returns(new Response(status, null, null));
        return client;
    }

    private static MailRequest ValidRequest() =>
        new(to: ["dest@x.com"], subject: "hi", body: "body");

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]        // 429 — превышен лимит запросов
    [InlineData(HttpStatusCode.InternalServerError)]    // 500 — на стороне SendGrid
    [InlineData(HttpStatusCode.ServiceUnavailable)]     // 503 — на стороне SendGrid
    public async Task SendAsync_When_TransientFailure_Should_Throw_ForRetry(HttpStatusCode status)
    {
        var service = BuildService(ClientReturning(status));

        var send = async () => await service.SendAsync(ValidRequest(), CancellationToken.None);

        // Исключение направляет отправку обратно через автоматический повтор Hangfire у вызывающего кода.
        await send.ShouldThrowAsync<Exception>();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]   // 401 — неверный API-ключ
    [InlineData(HttpStatusCode.BadRequest)]     // 400 — отклонённый получатель / некорректный формат
    [InlineData(HttpStatusCode.Forbidden)]      // 403 — отправитель не подтверждён
    public async Task SendAsync_When_PermanentRejection_Should_NotThrow_ToAvoidRetryStorm(HttpStatusCode status)
    {
        var service = BuildService(ClientReturning(status));

        var send = async () => await service.SendAsync(ValidRequest(), CancellationToken.None);

        // Логируется как ошибка (видна эксплуатации), но не выбрасывается — повтор не сможет привести к успеху.
        await send.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task SendAsync_When_SendGridReturnsAccepted_Should_Complete()
    {
        var service = BuildService(ClientReturning(HttpStatusCode.Accepted));

        var send = async () => await service.SendAsync(ValidRequest(), CancellationToken.None);

        await send.ShouldNotThrowAsync();
    }
}
