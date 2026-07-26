using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Multitenancy.Contracts;
using EDV.Modules.Multitenancy.Contracts.v1.ResetTenantTheme;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;

namespace EDV.Modules.Multitenancy.Features.v1.ResetTenantTheme;

public sealed class ResetTenantThemeCommandHandler(
    ITenantThemeService themeService,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : ICommandHandler<ResetTenantThemeCommand>
{
    public async ValueTask<Unit> Handle(ResetTenantThemeCommand command, CancellationToken cancellationToken)
    {
        var tenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new InvalidOperationException("Контекст тенанта недоступен");

        await themeService.ResetThemeAsync(tenantId, cancellationToken);

        return Unit.Value;
    }
}