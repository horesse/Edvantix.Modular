using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Contracts.v1.Wallets;
using EDV.Modules.Billing.Data;
using EDV.Modules.Billing.Services;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Billing.Features.v1.Wallets.ApproveTopupRequest;

public sealed class ApproveTopupRequestCommandHandler(
    BillingDbContext db,
    IBillingService billing,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : ICommandHandler<ApproveTopupRequestCommand, Guid>
{
    public async ValueTask<Guid> Handle(ApproveTopupRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var callerTenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Требуется контекст тенанта.");
        var isRoot = callerTenantId == MultitenancyConstants.Root.Id;

        var request = await db.TopupRequests
            .FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Запрос на пополнение {command.Id} не найден.");

        if (!isRoot && request.TenantId != callerTenantId)
        {
            throw new UnauthorizedException("Вы можете одобрять запросы на пополнение только для своего тенанта.");
        }

        // Для root операция выполняется над тенантом самого запроса; для не-root callerTenantId равен request.TenantId.
        var invoice = await billing.CreateTopupInvoiceAsync(request.TenantId, command.Id, cancellationToken)
            .ConfigureAwait(false);

        return invoice.Id;
    }
}
