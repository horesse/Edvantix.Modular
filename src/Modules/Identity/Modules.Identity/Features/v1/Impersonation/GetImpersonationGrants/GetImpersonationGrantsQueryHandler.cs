using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Impersonation;
using EDV.Modules.Identity.Contracts.v1.Impersonation.GetImpersonationGrants;
using Mediator;

namespace EDV.Modules.Identity.Features.v1.Impersonation.GetImpersonationGrants;

public sealed class GetImpersonationGrantsQueryHandler(
    IImpersonationGrantService grantService,
    ICurrentUser currentUser)
    : IQueryHandler<GetImpersonationGrantsQuery, IReadOnlyList<ImpersonationGrantDto>>
{
    public async ValueTask<IReadOnlyList<ImpersonationGrantDto>> Handle(
        GetImpersonationGrantsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var callerTenant = currentUser.GetTenant()
            ?? throw new UnauthorizedException("отсутствует контекст арендатора");
        var isRoot = string.Equals(callerTenant, MultitenancyConstants.Root.Id, StringComparison.Ordinal);

        // Ограничение по арендатору: корневые операторы нацеливаются на любого арендатора; администраторы
        // арендатора закреплены за своим, независимо от ввода. Отражает межарендаторное правило StartImpersonation.
        var tenantFilter = isRoot ? request.ImpersonatedTenantId : callerTenant;

        return await grantService.ListAsync(
            status: request.Status,
            impersonatedTenantId: tenantFilter,
            actorUserId: request.ActorUserId,
            take: request.Take,
            ct: cancellationToken).ConfigureAwait(false);
    }
}
