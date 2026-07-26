using EDV.Framework.Core.Context;
using EDV.Framework.Eventing.Outbox;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Auditing.Contracts;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.Events;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace EDV.Modules.Identity.Features.v1.Tokens.TokenGeneration;

public sealed class GenerateTokenCommandHandler
    : ICommandHandler<GenerateTokenCommand, TokenResponse>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly ISecurityAudit _securityAudit;
    private readonly IRequestContext _requestContext;
    private readonly IOutboxStore _outboxStore;
    private readonly IMultiTenantContextAccessor<AppTenantInfo> _multiTenantContextAccessor;
    private readonly ISessionService _sessionService;
    private readonly ILogger<GenerateTokenCommandHandler> _logger;

    public GenerateTokenCommandHandler(
        IIdentityService identityService,
        ITokenService tokenService,
        ISecurityAudit securityAudit,
        IRequestContext requestContext,
        IOutboxStore outboxStore,
        IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
        ISessionService sessionService,
        ILogger<GenerateTokenCommandHandler> logger)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _securityAudit = securityAudit;
        _requestContext = requestContext;
        _outboxStore = outboxStore;
        _multiTenantContextAccessor = multiTenantContextAccessor;
        _sessionService = sessionService;
        _logger = logger;
    }

    public async ValueTask<TokenResponse> Handle(
        GenerateTokenCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Собираем контекст для аудита
        var ip = _requestContext.IpAddress ?? "unknown";
        var ua = _requestContext.UserAgent ?? "unknown";
        var clientId = _requestContext.ClientId;

        // Проверяем учётные данные (включает проверку 2FA, если у пользователя она включена)
        var identityResult = await _identityService
            .ValidateCredentialsAsync(request.Email, request.Password, request.TwoFactorCode, cancellationToken);

        if (identityResult is null)
        {
            // 1) Фиксируем в аудите неудачный вход ДО выбрасывания исключения
            await _securityAudit.LoginFailedAsync(
                subjectIdOrName: request.Email,
                clientId: clientId!,
                reason: "InvalidCredentials",
                ip: ip,
                ct: cancellationToken);

            throw new UnauthorizedAccessException("Неверные учётные данные.");
        }

        // Распаковываем subject + claims
        var (subject, claims) = identityResult.Value;

        // 2) Фиксируем в аудите успешный вход
        await _securityAudit.LoginSucceededAsync(
            userId: subject,
            userName: claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? request.Email,
            clientId: clientId!,
            ip: ip,
            userAgent: ua,
            ct: cancellationToken);

        // Выпускаем токен
        var token = await _tokenService.IssueAsync(subject, claims, /*extra*/ null, cancellationToken);

        // Сохраняем refresh-токен (хешированный) для этого пользователя
        await _identityService.StoreRefreshTokenAsync(subject, token.RefreshToken, token.RefreshTokenExpiresAt, cancellationToken);

        // Создаём сессию пользователя для управления сессиями (неблокирующе, с плавным сбоем)
        try
        {
            var refreshTokenHash = Sha256Short(token.RefreshToken);
            await _sessionService.CreateSessionAsync(
                subject,
                refreshTokenHash,
                ip,
                ua,
                token.RefreshTokenExpiresAt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Создание сессии некритично — не проваливаем вход
            // Это может случиться, если миграции ещё не применены
            _logger.LogWarning(ex, "Не удалось создать сессию пользователя для {UserId}. Вход продолжится без отслеживания сессии.", subject);
        }

        // 3) Фиксируем в аудите выпуск токена по отпечатку (никогда сырой токен)
        var fingerprint = Sha256Short(token.AccessToken);
        await _securityAudit.TokenIssuedAsync(
            userId: subject,
            userName: claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? request.Email,
            clientId: clientId!,
            tokenFingerprint: fingerprint,
            expiresUtc: token.AccessTokenExpiresAt,
            ct: cancellationToken);

        // 4) Ставим в очередь интеграционное событие генерации токена (тестовое событие для проверки eventing)
        var tenantId = _multiTenantContextAccessor.MultiTenantContext?.TenantInfo?.Id;
        var correlationId = Guid.NewGuid().ToString();

        var integrationEvent = new TokenGeneratedIntegrationEvent(
            Id: Guid.NewGuid(),
            OccurredOnUtc: TimeProvider.System.GetUtcNow().UtcDateTime,
            TenantId: tenantId,
            CorrelationId: correlationId,
            Source: "Identity",
            UserId: subject,
            Email: request.Email,
            ClientId: clientId!,
            IpAddress: ip,
            UserAgent: ua,
            TokenFingerprint: fingerprint,
            AccessTokenExpiresAtUtc: token.AccessTokenExpiresAt);

        await _outboxStore.AddAsync(integrationEvent, cancellationToken).ConfigureAwait(false);

        return token;
    }

    private static string Sha256Short(string value)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        // короткий печатаемый отпечаток; хранится только он
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }
}