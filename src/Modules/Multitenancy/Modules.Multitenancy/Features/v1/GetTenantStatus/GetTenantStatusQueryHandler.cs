using EDV.Modules.Multitenancy.Contracts;
using EDV.Modules.Multitenancy.Contracts.Dtos;
using EDV.Modules.Multitenancy.Contracts.v1.GetTenantStatus;
using Mediator;

namespace EDV.Modules.Multitenancy.Features.v1.GetTenantStatus;

public sealed class GetTenantStatusQueryHandler(ITenantService tenantService)
    : IQueryHandler<GetTenantStatusQuery, TenantStatusDto>
{
    public async ValueTask<TenantStatusDto> Handle(GetTenantStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await tenantService.GetStatusAsync(query.TenantId, cancellationToken).ConfigureAwait(false);
    }
}