using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Multitenancy.Contracts;
using EDV.Modules.Multitenancy.Contracts.v1.UpdateTenantTheme;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;

namespace EDV.Modules.Multitenancy.Features.v1.UpdateTenantTheme;

public sealed class UpdateTenantThemeCommandHandler(
    ITenantThemeService themeService,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : ICommandHandler<UpdateTenantThemeCommand>
{
    public async ValueTask<Unit> Handle(UpdateTenantThemeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new InvalidOperationException("Контекст тенанта недоступен");

        await themeService.UpdateThemeAsync(tenantId, command.Theme, cancellationToken);

        return Unit.Value;
    }
}