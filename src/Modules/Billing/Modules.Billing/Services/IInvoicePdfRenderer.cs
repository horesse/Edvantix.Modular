using EDV.Modules.Billing.Contracts.Dtos;

namespace EDV.Modules.Billing.Services;

/// <summary>Рендерит счёт в самодостаточный PDF-документ (по требованию, без сохранения артефакта).</summary>
public interface IInvoicePdfRenderer
{
    byte[] Render(InvoiceDto invoice);
}
