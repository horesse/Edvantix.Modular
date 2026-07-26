using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Identity;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Auditing.Contracts;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Impersonation;
using EDV.Modules.Identity.Contracts.v1.Impersonation.StartImpersonation;
using Mediator;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EDV.Modules.Identity.Features.v1.Impersonation.StartImpersonation;

public sealed class StartImpersonationCommandHandler
    : ICommandHandler<StartImpersonationCommand, ImpersonationResponse>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly ISecurityAudit _securityAudit;
    private readonly ICurrentUser _currentUser;
    private readonly IRequestContext _requestContext;
    private readonly IImpersonationGrantService _grantService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StartImpersonationCommandHandler> _logger;

    public StartImpersonationCommandHandler(
        IIdentityService identityService,
        ITokenService tokenService,
        ISecurityAudit securityAudit,
        ICurrentUser currentUser,
        IRequestContext requestContext,
        IImpersonationGrantService grantService,
        TimeProvider timeProvider,
        ILogger<StartImpersonationCommandHandler> logger)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _securityAudit = securityAudit;
        _currentUser = currentUser;
        _requestContext = requestContext;
        _grantService = grantService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async ValueTask<ImpersonationResponse> Handle(
        StartImpersonationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUser.IsAuthenticated())
        {
            throw new UnauthorizedException();
        }

        var actorUserId = _currentUser.GetUserId().ToString();
        var actorTenantId = _currentUser.GetTenant()
            ?? throw new UnauthorizedException("отсутствует контекст арендатора");
        var actorUserName = _currentUser.Name;

        // Межарендаторная имперсонализация требует, чтобы актор находился в корневом арендаторе.
        // Администраторы арендатора могут имперсонализировать только пользователей своего арендатора.
        if (!string.Equals(actorTenantId, MultitenancyConstants.Root.Id, StringComparison.Ordinal)
            && !string.Equals(actorTenantId, request.TargetTenantId, StringComparison.Ordinal))
        {
            throw new ForbiddenException("межарендаторная имперсонализация доступна только операторам платформы");
        }

        // Предотвращаем самоимперсонализацию (бессмысленно, запутывает след аудита). Ошибка вызывающего →
        // явный 4xx, а не 500, используемый CustomException по умолчанию.
        if (string.Equals(actorUserId, request.TargetUserId, StringComparison.Ordinal)
            && string.Equals(actorTenantId, request.TargetTenantId, StringComparison.Ordinal))
        {
            throw new CustomException("нельзя имперсонализировать самого себя", errors: null, System.Net.HttpStatusCode.BadRequest);
        }

        // Предотвращаем вложенность: если вызывающий уже имперсонализирует, сначала требуем завершить текущую.
        var callerClaims = _currentUser.GetUserClaims();
        if (callerClaims is not null
            && callerClaims.Any(c => c.Type == ClaimConstants.ActorSubject))
        {
            throw new CustomException(
                "завершите текущую имперсонализацию перед началом новой",
                errors: null,
                System.Net.HttpStatusCode.BadRequest);
        }

        var targetClaimsResult = await _identityService
            .BuildClaimsForUserAsync(request.TargetUserId, request.TargetTenantId, cancellationToken);

        if (targetClaimsResult is null)
        {
            throw new NotFoundException("целевой пользователь не найден");
        }

        var (subject, claims) = targetClaimsResult.Value;
        var targetUserName = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value
            ?? claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value;

        // Убираем автосгенерированный jti из BuildClaimsForUserAsync и подставляем свой, чтобы сохранённая
        // строка ImpersonationGrant и выпущенный JWT использовали один и тот же jti.
        var jti = Guid.NewGuid().ToString("N");
        var impersonationClaims = claims
            .Where(c => c.Type != JwtRegisteredClaimNames.Jti)
            .Concat(
            [
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                // claims актора по RFC 8693, чтобы выпущенный токен нёс информацию о том, кто действует.
                new Claim(ClaimConstants.ActorSubject, actorUserId),
                new Claim(ClaimConstants.ActorTenant, actorTenantId)
            ])
            .ToList();

        // Ограничиваем переданную вызывающим длительность на сервере (защита в глубину: валидатор уже
        // отклоняет значения вне диапазона, но будущий вызывающий, обошедший его, не должен выйти за предел).
        var lifetime = request.DurationMinutes is { } minutes
            ? TimeSpan.FromMinutes(Math.Clamp(minutes, 1, StartImpersonationCommandValidator.MaxImpersonationMinutes))
            : (TimeSpan?)null;

        var startedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var (accessToken, expiresAt) = await _tokenService.IssueAccessOnlyAsync(
            subject, impersonationClaims, lifetime, cancellationToken);

        // Сохраняем грант ПОСЛЕ выпуска токена, чтобы неудачный выпуск не оставлял осиротевший грант.
        // CreateAsync прогревает кэш, чтобы хук валидации JWT увидел status=Active на следующем запросе без обращения к БД.
        await _grantService.CreateAsync(new CreateGrantInput(
            Jti: jti,
            ActorUserId: actorUserId,
            ActorUserName: actorUserName,
            ActorTenantId: actorTenantId,
            ImpersonatedUserId: subject,
            ImpersonatedUserName: targetUserName,
            ImpersonatedTenantId: request.TargetTenantId,
            Reason: request.Reason ?? string.Empty,
            StartedAtUtc: startedAtUtc,
            ExpiresAtUtc: expiresAt,
            ClientId: _requestContext.ClientId,
            IpAddress: _requestContext.IpAddress,
            UserAgent: _requestContext.UserAgent), cancellationToken);

        await _securityAudit.ImpersonationStartedAsync(
            actorUserId: actorUserId,
            actorTenantId: actorTenantId,
            targetUserId: subject,
            targetTenantId: request.TargetTenantId,
            clientId: _requestContext.ClientId ?? "unknown",
            ip: _requestContext.IpAddress ?? "unknown",
            userAgent: _requestContext.UserAgent ?? "unknown",
            reason: request.Reason ?? string.Empty,
            ct: cancellationToken);

        _logger.LogWarning(
            "Имперсонализация начата: актор {ActorUserId}@{ActorTenant} -> цель {TargetUserId}@{TargetTenant} jti={Jti}",
            actorUserId, actorTenantId, subject, request.TargetTenantId, jti);

        return new ImpersonationResponse(
            AccessToken: accessToken,
            AccessTokenExpiresAt: expiresAt,
            ActorUserId: actorUserId,
            ActorTenantId: actorTenantId,
            ImpersonatedUserId: subject,
            ImpersonatedTenantId: request.TargetTenantId);
    }
}
