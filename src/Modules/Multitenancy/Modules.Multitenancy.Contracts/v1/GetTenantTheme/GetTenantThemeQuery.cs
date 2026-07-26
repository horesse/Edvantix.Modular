using EDV.Modules.Multitenancy.Contracts.Dtos;
using Mediator;

namespace EDV.Modules.Multitenancy.Contracts.v1.GetTenantTheme;

public sealed record GetTenantThemeQuery : IQuery<TenantThemeDto>;