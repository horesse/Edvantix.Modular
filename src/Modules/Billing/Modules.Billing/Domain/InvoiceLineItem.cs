using EDV.Framework.Core.Domain;
using EDV.Framework.Shared.Quota;
using EDV.Modules.Billing.Contracts;

namespace EDV.Modules.Billing.Domain;

/// <summary>
/// Одна строка счёта. Сумма вычисляется как Quantity * UnitPrice и сохраняется, чтобы суммирование
/// для <see cref="Invoice.SubtotalAmount"/> не требовало каждый раз повторного обхода строк.
/// </summary>
public sealed class InvoiceLineItem : BaseEntity<Guid>
{
    public Guid InvoiceId { get; private set; }
    public InvoiceLineItemKind Kind { get; private set; }
    public QuotaResource? Resource { get; private set; }
    public string Description { get; private set; } = default!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public Money Amount { get; private set; } = default!;

    private InvoiceLineItem() { }

    internal static InvoiceLineItem Create(
        Guid invoiceId,
        InvoiceLineItemKind kind,
        string description,
        decimal quantity,
        decimal unitPrice,
        string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Количество не может быть отрицательным.");
        }
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Цена за единицу не может быть отрицательной.");
        }

        return new InvoiceLineItem
        {
            Id = Guid.CreateVersion7(),
            InvoiceId = invoiceId,
            Kind = kind,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Amount = new Money(quantity * unitPrice, currency).Round(2)
        };
    }

    internal void AttachResource(QuotaResource resource) => Resource = resource;
}
