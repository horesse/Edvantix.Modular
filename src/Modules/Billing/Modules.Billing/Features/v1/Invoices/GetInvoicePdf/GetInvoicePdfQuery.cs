using Mediator;

namespace EDV.Modules.Billing.Features.v1.Invoices.GetInvoicePdf;

/// <summary>Получает счёт тенанта вызывающего и рендерит его в PDF. Внутренний для модуля (результат
/// в виде byte[] не является межмодульным контрактом).</summary>
public sealed record GetInvoicePdfQuery(Guid InvoiceId) : IQuery<InvoicePdfResult>;

public sealed record InvoicePdfResult(byte[] Content, string FileName);
