using EDV.Framework.Shared.Persistence;
using EDV.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Roles.GetRoles;

public sealed class GetRolesQuery : IPagedQuery, IQuery<PagedResponse<RoleDto>>
{
    public int? PageNumber { get; set; }

    public int? PageSize { get; set; }

    public string? Sort { get; set; }

    /// <summary>Поиск подстроки без учёта регистра по имени роли и описанию.</summary>
    public string? Search { get; set; }
}