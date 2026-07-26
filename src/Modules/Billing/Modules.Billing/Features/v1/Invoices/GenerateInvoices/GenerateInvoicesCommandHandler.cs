using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Contracts.v1.Invoices;
using EDV.Modules.Billing.Services;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;

namespace EDV.Modules.Billing.Features.v1.Invoices.GenerateInvoices;

public sealed class GenerateInvoicesCommandHandler(
    IBillingService billing,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : ICommandHandler<GenerateInvoicesCommand, int>
{
    public async ValueTask<int> Handle(GenerateInvoicesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Генерация счетов в масштабе платформы затрагивает КАЖДОГО тенанта — это действие root-оператора.
        // Администратор тенанта (даже с правом Billing.Manage) не должен иметь возможность его запускать.
        var callerTenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Требуется контекст тенанта.");
        if (callerTenantId != MultitenancyConstants.Root.Id)
        {
            throw new ForbiddenException("Генерировать счета по всем тенантам может только root-оператор.");
        }

        return await billing.GenerateInvoicesForAllTenantsAsync(command.PeriodYear, command.PeriodMonth, cancellationToken).ConfigureAwait(false);
    }
}
