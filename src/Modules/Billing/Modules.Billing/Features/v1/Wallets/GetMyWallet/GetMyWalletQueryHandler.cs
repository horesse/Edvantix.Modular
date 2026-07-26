using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Contracts.Dtos;
using EDV.Modules.Billing.Contracts.v1.Wallets;
using EDV.Modules.Billing.Mappings;
using EDV.Modules.Billing.Services;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;

namespace EDV.Modules.Billing.Features.v1.Wallets.GetMyWallet;

public sealed class GetMyWalletQueryHandler(
    IBillingService billingService,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IQueryHandler<GetMyWalletQuery, WalletDto>
{
    public async ValueTask<WalletDto> Handle(GetMyWalletQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // BillingDbContext не фильтруется по тенанту; определяем собственный тенант вызывающего и строго ограничиваем область им.
        var tenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Требуется контекст тенанта.");

        var wallet = await billingService.GetOrCreateWalletAsync(tenantId, "USD", cancellationToken).ConfigureAwait(false);
        return wallet.ToDto();
    }
}
