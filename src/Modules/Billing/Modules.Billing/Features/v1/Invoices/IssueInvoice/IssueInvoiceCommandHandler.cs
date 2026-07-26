using EDV.Modules.Billing.Contracts.v1.Invoices;
using EDV.Modules.Billing.Services;
using Mediator;

namespace EDV.Modules.Billing.Features.v1.Invoices.IssueInvoice;

public sealed class IssueInvoiceCommandHandler(IBillingService billing)
    : ICommandHandler<IssueInvoiceCommand, Guid>
{
    public async ValueTask<Guid> Handle(IssueInvoiceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await billing.IssueInvoiceAsync(command.InvoiceId, command.DueAtUtc, cancellationToken).ConfigureAwait(false);
        return command.InvoiceId;
    }
}
