using EDV.Modules.Identity.Authorization.Jwt;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EDV.Modules.Identity.Services;

public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly ILogger<TokenService> _logger;
    private readonly IdentityMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public TokenService(IOptions<JwtOptions> options, ILogger<TokenService> logger, IdentityMetrics metrics, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public Task<TokenResponse> IssueAsync(
        string subject,
        IEnumerable<Claim> claims,
        string? tenant = null,
        CancellationToken ct = default)
    {
        var (accessToken, accessTokenExpiry) = BuildAccessToken(subject, claims, lifetime: null);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var refreshTokenExpiry = now.AddDays(_options.RefreshTokenDays);

        var response = new TokenResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            RefreshTokenExpiresAt: refreshTokenExpiry,
            AccessTokenExpiresAt: accessTokenExpiry);

        return Task.FromResult(response);
    }

    public Task<(string AccessToken, DateTime ExpiresAtUtc)> IssueAccessOnlyAsync(
        string subject,
        IEnumerable<Claim> claims,
        TimeSpan? lifetime = null,
        CancellationToken ct = default)
    {
        var result = BuildAccessToken(subject, claims, lifetime);
        return Task.FromResult(result);
    }

    private (string AccessToken, DateTime ExpiresAtUtc) BuildAccessToken(
        string subject,
        IEnumerable<Claim> claims,
        TimeSpan? lifetime = null)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        // Время жизни, переданное вызывающим кодом, имеет приоритет над настроенным по умолчанию;
        // вызывающий код (например, StartImpersonationCommandHandler) обязан ограничить его безопасным верхним пределом.
        var accessTokenExpiry = lifetime is { } span
            ? now.Add(span)
            : now.AddMinutes(_options.AccessTokenMinutes);
        var jwtToken = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            expires: accessTokenExpiry,
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwtToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Выпущен JWT для субъекта {Subject}", subject);
        }
        _metrics.TokenGenerated(subject);

        return (accessToken, accessTokenExpiry);
    }
}