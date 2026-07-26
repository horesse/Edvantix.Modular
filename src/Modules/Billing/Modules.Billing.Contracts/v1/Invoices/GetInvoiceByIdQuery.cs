using EDV.Modules.Billing.Contracts.Dtos;
using Mediator;

namespace EDV.Modules.Billing.Contracts.v1.Invoices;

public sealed record GetInvoiceByIdQuery(Guid InvoiceId) : IQuery<InvoiceDto>;
