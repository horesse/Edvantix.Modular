using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Modules.Auditing.Contracts;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Tokens.RefreshToken;
using Mediator;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EDV.Modules.Identity.Features.v1.Tokens.RefreshToken;

public sealed class RefreshTokenCommandHandler
    : ICommandHandler<RefreshTokenCommand, RefreshTokenCommandResponse>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly ISecurityAudit _securityAudit;
    private readonly IRequestContext _requestContext;
    private readonly ISessionService _sessionService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IIdentityService identityService,
        ITokenService tokenService,
        ISecurityAudit securityAudit,
        IRequestContext requestContext,
        ISessionService sessionService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _securityAudit = securityAudit;
        _requestContext = requestContext;
        _sessionService = sessionService;
        _logger = logger;
    }

    public async ValueTask<RefreshTokenCommandResponse> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var clientId = _requestContext.ClientId;

        // Проверяем refresh-токен и пересобираем subject + claims
        var validated = await _identityService
            .ValidateRefreshTokenAsync(request.RefreshToken, cancellationToken);

        if (validated is null)
        {
            await _securityAudit.TokenRevokedAsync("unknown", clientId!, "InvalidRefreshToken", cancellationToken);
            throw new UnauthorizedException("Недействительный refresh-токен.");
        }

        var (subject, claims) = validated.Value;

        // Проверяем, что сессия, связанная с этим refresh-токеном, всё ещё действительна
        var refreshTokenHash = Sha256Short(request.RefreshToken);
        var isSessionValid = await _sessionService.ValidateSessionAsync(refreshTokenHash, cancellationToken);
        if (!isSessionValid)
        {
            await _securityAudit.TokenRevokedAsync(subject, clientId!, "SessionRevoked", cancellationToken);
            throw new UnauthorizedException("Сессия была отозвана.");
        }

        // При необходимости сверяем subject предоставленного access-токена
        var handler = new JwtSecurityTokenHandler();
        JwtSecurityToken? parsedAccessToken = null;
        try
        {
            parsedAccessToken = handler.ReadJwtToken(request.Token);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось разобрать access-токен при обновлении; полагаемся только на проверку refresh-токена");
        }

        if (parsedAccessToken is not null)
        {
            var accessTokenSubject = parsedAccessToken.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                ?? parsedAccessToken.Subject;

            if (!string.IsNullOrEmpty(accessTokenSubject) &&
                !string.Equals(accessTokenSubject, subject, StringComparison.Ordinal))
            {
                await _securityAudit.TokenRevokedAsync(subject, clientId!, "RefreshTokenSubjectMismatch", cancellationToken);
                throw new UnauthorizedException("Несовпадение subject access-токена.");
            }
        }

        // Фиксируем в аудите отзыв предыдущего токена по причине ротации (без сырых токенов)
        await _securityAudit.TokenRevokedAsync(subject, clientId!, "RefreshTokenRotated", cancellationToken);

        // Выпускаем новые токены
        var newToken = await _tokenService.IssueAsync(subject, claims, null, cancellationToken);

        // Сохраняем повёрнутый refresh-токен для этого пользователя
        await _identityService.StoreRefreshTokenAsync(subject, newToken.RefreshToken, newToken.RefreshTokenExpiresAt, cancellationToken);

        // Обновляем сессию новым хешем refresh-токена
        var newRefreshTokenHash = Sha256Short(newToken.RefreshToken);
        await _sessionService.UpdateSessionRefreshTokenAsync(
            refreshTokenHash,
            newRefreshTokenHash,
            newToken.RefreshTokenExpiresAt,
            cancellationToken);

        // Фиксируем в аудите только что выпущенный токен по отпечатку
        var fingerprint = Sha256Short(newToken.AccessToken);
        await _securityAudit.TokenIssuedAsync(
            userId: subject,
            userName: claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty,
            clientId: clientId!,
            tokenFingerprint: fingerprint,
            expiresUtc: newToken.AccessTokenExpiresAt,
            ct: cancellationToken);

        return new RefreshTokenCommandResponse(
            Token: newToken.AccessToken,
            RefreshToken: newToken.RefreshToken,
            RefreshTokenExpiryTime: newToken.RefreshTokenExpiresAt);
    }

    private static string Sha256Short(string value)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }
}