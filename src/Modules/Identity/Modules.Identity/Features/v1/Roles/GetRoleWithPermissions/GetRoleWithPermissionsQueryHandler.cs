using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Roles.GetRoleWithPermissions;
using Mediator;

namespace EDV.Modules.Identity.Features.v1.Roles.GetRoleWithPermissions;

public sealed class GetRoleWithPermissionsQueryHandler : IQueryHandler<GetRoleWithPermissionsQuery, RoleDto>
{
    private readonly IRoleService _roleService;

    public GetRoleWithPermissionsQueryHandler(IRoleService roleService)
    {
        _roleService = roleService;
    }

    public async ValueTask<RoleDto> Handle(GetRoleWithPermissionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await _roleService.GetWithPermissionsAsync(query.Id, cancellationToken).ConfigureAwait(false);
    }
}