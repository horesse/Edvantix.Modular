using EDV.Modules.Multitenancy.Contracts.Dtos;
using Mediator;

namespace EDV.Modules.Multitenancy.Contracts.v1.UpdateTenantTheme;

public sealed record UpdateTenantThemeCommand(TenantThemeDto Theme) : ICommand;