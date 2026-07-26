using EDV.Framework.Eventing.Abstractions;
using EDV.Modules.Identity.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Identity.Events;

/// <summary>
/// Пример обработчика, логирующего факт генерации токена.
/// В первую очередь предназначен для упрощения тестирования конвейера интеграционных событий.
/// </summary>
public sealed class TokenGeneratedLogHandler
    : IIntegrationEventHandler<TokenGeneratedIntegrationEvent>
{
    private readonly ILogger<TokenGeneratedLogHandler> _logger;

    public TokenGeneratedLogHandler(ILogger<TokenGeneratedLogHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(TokenGeneratedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            // Минимизация PII: логируем только псевдонимный UserId, без адреса электронной почты.
            _logger.LogInformation(
                "Токен сгенерирован для пользователя {UserId}, клиент {ClientId}, IP {IpAddress}, UserAgent {UserAgent}, истекает {ExpiresAtUtc} (отпечаток: {Fingerprint})",
                @event.UserId,
                @event.ClientId,
                @event.IpAddress,
                @event.UserAgent,
                @event.AccessTokenExpiresAtUtc,
                @event.TokenFingerprint);
        }

        return Task.CompletedTask;
    }
}