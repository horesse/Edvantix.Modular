using EDV.Modules.Multitenancy.Contracts;
using EDV.Modules.Multitenancy.Contracts.Dtos;
using EDV.Modules.Multitenancy.Contracts.v1.GetTenantTheme;
using Mediator;

namespace EDV.Modules.Multitenancy.Features.v1.GetTenantTheme;

public sealed class GetTenantThemeQueryHandler(ITenantThemeService themeService)
    : IQueryHandler<GetTenantThemeQuery, TenantThemeDto>
{
    public async ValueTask<TenantThemeDto> Handle(GetTenantThemeQuery query, CancellationToken cancellationToken)
    {
        return await themeService.GetCurrentTenantThemeAsync(cancellationToken);
    }
}