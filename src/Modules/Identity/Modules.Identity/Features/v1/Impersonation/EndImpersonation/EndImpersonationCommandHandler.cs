using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Identity;
using EDV.Modules.Auditing.Contracts;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Impersonation.EndImpersonation;
using Mediator;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;

namespace EDV.Modules.Identity.Features.v1.Impersonation.EndImpersonation;

public sealed class EndImpersonationCommandHandler
    : ICommandHandler<EndImpersonationCommand, TokenResponse>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly ISecurityAudit _securityAudit;
    private readonly ICurrentUser _currentUser;
    private readonly IRequestContext _requestContext;
    private readonly IImpersonationGrantService _grantService;
    private readonly ILogger<EndImpersonationCommandHandler> _logger;

    public EndImpersonationCommandHandler(
        IIdentityService identityService,
        ITokenService tokenService,
        ISecurityAudit securityAudit,
        ICurrentUser currentUser,
        IRequestContext requestContext,
        IImpersonationGrantService grantService,
        ILogger<EndImpersonationCommandHandler> logger)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _securityAudit = securityAudit;
        _currentUser = currentUser;
        _requestContext = requestContext;
        _grantService = grantService;
        _logger = logger;
    }

    public async ValueTask<TokenResponse> Handle(
        EndImpersonationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUser.IsAuthenticated())
        {
            throw new UnauthorizedException();
        }

        var claims = _currentUser.GetUserClaims()?.ToList()
            ?? throw new UnauthorizedException();

        var actorUserId = claims.FirstOrDefault(c => c.Type == ClaimConstants.ActorSubject)?.Value;
        var actorTenantId = claims.FirstOrDefault(c => c.Type == ClaimConstants.ActorTenant)?.Value;
        var jti = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

        if (string.IsNullOrWhiteSpace(actorUserId) || string.IsNullOrWhiteSpace(actorTenantId))
        {
            // Вход выполнен, но нет claim act_sub (End вызван для токена без имперсонализации): ошибка клиента,
            // должна быть 4xx, а не 500 по умолчанию для CustomException.
            throw new CustomException(
                "текущая сессия не является сессией имперсонализации",
                errors: null,
                System.Net.HttpStatusCode.BadRequest);
        }

        var impersonatedUserId = _currentUser.GetUserId().ToString();
        var impersonatedTenantId = _currentUser.GetTenant() ?? string.Empty;

        // Помечаем грант завершённым ДО выдачи токенов актора, чтобы конкурирующий запрос хука JWT увидел
        // "завершён" (безопаснее, чем наоборот). Если MarkEnded не удался, всё равно продолжаем: грант
        // истечёт естественным образом, а хук трактует неизвестные состояния как отозванные.
        if (!string.IsNullOrWhiteSpace(jti))
        {
            try
            {
                await _grantService.MarkEndedByJtiAsync(jti, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Не удалось пометить грант имперсонализации завершённым для jti={Jti}. Замена актора всё равно продолжится.",
                    jti);
            }
        }

        var actorClaimsResult = await _identityService
            .BuildClaimsForUserAsync(actorUserId, actorTenantId, cancellationToken);

        if (actorClaimsResult is null)
        {
            throw new NotFoundException("исходный актор не найден");
        }

        var (subject, actorClaims) = actorClaimsResult.Value;

        var token = await _tokenService.IssueAsync(subject, actorClaims, actorTenantId, cancellationToken);
        await _identityService.StoreRefreshTokenAsync(subject, token.RefreshToken, token.RefreshTokenExpiresAt, cancellationToken);

        await _securityAudit.ImpersonationEndedAsync(
            actorUserId: actorUserId,
            actorTenantId: actorTenantId,
            targetUserId: impersonatedUserId,
            targetTenantId: impersonatedTenantId,
            clientId: _requestContext.ClientId ?? "unknown",
            ct: cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Имперсонализация завершена: актор {ActorUserId}@{ActorTenant} вернулся из {TargetUserId}@{TargetTenant} jti={Jti}",
                actorUserId, actorTenantId, impersonatedUserId, impersonatedTenantId, jti ?? "<missing>");
        }

        return token;
    }
}
