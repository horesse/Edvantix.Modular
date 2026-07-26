using EDV.Framework.Shared.Identity;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.v1.Permissions.GetPermissionCatalog;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;

namespace EDV.Modules.Identity.Features.v1.Permissions.GetPermissionCatalog;

public sealed class GetPermissionCatalogQueryHandler(
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IQueryHandler<GetPermissionCatalogQuery, IReadOnlyList<PermissionCatalogEntryDto>>
{
    public ValueTask<IReadOnlyList<PermissionCatalogEntryDto>> Handle(
        GetPermissionCatalogQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tenantId = tenantAccessor.MultiTenantContext.TenantInfo?.Id;
        bool isRoot = string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.Ordinal);

        // Соответствует тому же правилу root-vs-admin, что использует RolePermissionSyncer, чтобы каталог,
        // который редактирует SPA, совпадал с набором, который синхронизатор внёс бы в claims ролей арендатора.
        var source = isRoot
            ? PermissionConstants.Admin.Concat(PermissionConstants.Root).DistinctBy(p => p.Name)
            : PermissionConstants.Admin;

        IReadOnlyList<PermissionCatalogEntryDto> result =
        [
            .. source.Select(p => new PermissionCatalogEntryDto(
                p.Name,
                p.Description,
                p.Resource,
                p.Action,
                p.IsBasic,
                p.IsRoot))
        ];

        return ValueTask.FromResult(result);
    }
}
