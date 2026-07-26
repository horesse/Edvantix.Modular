using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Auditing.Contracts;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Impersonation;
using EDV.Modules.Identity.Contracts.v1.Impersonation.RevokeImpersonationGrant;
using Mediator;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Identity.Features.v1.Impersonation.RevokeImpersonationGrant;

public sealed class RevokeImpersonationGrantCommandHandler(
    IImpersonationGrantService grantService,
    ICurrentUser currentUser,
    ISecurityAudit securityAudit,
    IRequestContext requestContext,
    ILogger<RevokeImpersonationGrantCommandHandler> logger)
    : ICommandHandler<RevokeImpersonationGrantCommand, ImpersonationGrantDto>
{
    public async ValueTask<ImpersonationGrantDto> Handle(
        RevokeImpersonationGrantCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!currentUser.IsAuthenticated())
        {
            throw new UnauthorizedException();
        }

        var callerUserId = currentUser.GetUserId().ToString();
        var callerTenantId = currentUser.GetTenant()
            ?? throw new UnauthorizedException("отсутствует контекст арендатора");
        var isRoot = string.Equals(callerTenantId, MultitenancyConstants.Root.Id, StringComparison.Ordinal);

        // Проверяем видимость перед отзывом: администраторы арендатора могут отзывать только гранты
        // в своём арендаторе. Межарендаторные гранты возвращают 404 (а не 403), чтобы не подтверждать
        // существование вне области видимости.
        var grant = await grantService.GetByIdAsync(request.GrantId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("грант имперсонализации не найден");

        var withinTenant = string.Equals(grant.ImpersonatedTenantId, callerTenantId, StringComparison.Ordinal)
            || string.Equals(grant.ActorTenantId, callerTenantId, StringComparison.Ordinal);

        if (!isRoot && !withinTenant)
        {
            throw new NotFoundException("грант имперсонализации не найден");
        }

        var updated = await grantService.RevokeAsync(
            id: request.GrantId,
            revokedByUserId: callerUserId,
            revokedByUserName: currentUser.Name,
            reason: request.Reason,
            ct: cancellationToken).ConfigureAwait(false);

        // Отображаем отзыв как полноценное событие безопасности, доступное для запроса наряду с записями Start/End.
        // Reason в аудите — причина отзыва, а не исходная причина имперсонализации.
        await securityAudit.ImpersonationEndedAsync(
            actorUserId: grant.ActorUserId,
            actorTenantId: grant.ActorTenantId,
            targetUserId: grant.ImpersonatedUserId,
            targetTenantId: grant.ImpersonatedTenantId,
            clientId: requestContext.ClientId ?? "unknown",
            ct: cancellationToken).ConfigureAwait(false);

        logger.LogWarning(
            "Грант имперсонализации отозван: grantId={GrantId} jti={Jti} revokedBy={RevokedBy} reason={Reason}",
            updated.Id, updated.Jti, callerUserId, request.Reason ?? "<none>");

        return updated;
    }
}
