using EDV.Framework.Core.Domain;
using EDV.Modules.Billing.Contracts;

namespace EDV.Modules.Billing.Domain;

/// <summary>
/// Счёт тенанта за один месячный период. Начинается в статусе Draft, переходит в Issued при
/// отправке клиенту, затем в Paid или Void. Итоговые суммы пересчитываются при каждом добавлении
/// строки, чтобы вызывающему коду не приходилось делать это самостоятельно.
/// </summary>
public sealed class Invoice : AggregateRoot<Guid>
{
    private readonly List<InvoiceLineItem> _lineItems = new();

    public string TenantId { get; private set; } = default!;
    public string InvoiceNumber { get; private set; } = default!;
    public int PeriodYear { get; private set; }
    public int PeriodMonth { get; private set; }

    /// <summary>
    /// За что выставлен этот счёт. <see cref="Subscription"/> покрывает срок тарифа (создаётся
    /// при создании или продлении тенанта); <see cref="InvoicePurpose.Usage"/> покрывает учитываемый
    /// перерасход за месяц (создаётся ежемесячной задачей). Эти два потока никогда не пересекаются
    /// по ключам идемпотентности.
    /// </summary>
    public InvoicePurpose Purpose { get; private set; } = InvoicePurpose.Usage;

    /// <summary>Начало оплачиваемого срока (только для счетов по подписке).</summary>
    public DateTime? PeriodStartUtc { get; private set; }

    /// <summary>Конец оплачиваемого срока (только для счетов по подписке).</summary>
    public DateTime? PeriodEndUtc { get; private set; }
    public Money SubtotalAmount { get; private set; } = default!;
    public string Currency => SubtotalAmount.Currency;
    public InvoiceStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? IssuedAtUtc { get; private set; }
    public DateTime? DueAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public DateTime? VoidedAtUtc { get; private set; }
    public string? Notes { get; private set; }

    public IReadOnlyList<InvoiceLineItem> LineItems => _lineItems;

    private Invoice() { }

    public static Invoice CreateDraft(
        string tenantId,
        string invoiceNumber,
        int periodYear,
        int periodMonth,
        string currency)
        => CreateDraft(tenantId, invoiceNumber, periodYear, periodMonth, currency,
            InvoicePurpose.Usage, periodStartUtc: null, periodEndUtc: null);

    public static Invoice CreateDraft(
        string tenantId,
        string invoiceNumber,
        int periodYear,
        int periodMonth,
        string currency,
        InvoicePurpose purpose,
        DateTime? periodStartUtc,
        DateTime? periodEndUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (periodYear is < 2000 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(periodYear));
        }
        if (periodMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(periodMonth));
        }

        return new Invoice
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber,
            PeriodYear = periodYear,
            PeriodMonth = periodMonth,
            Purpose = purpose,
            PeriodStartUtc = periodStartUtc is { } s ? DateTime.SpecifyKind(s, DateTimeKind.Utc) : null,
            PeriodEndUtc = periodEndUtc is { } e ? DateTime.SpecifyKind(e, DateTimeKind.Utc) : null,
            SubtotalAmount = Money.Zero(currency.ToUpperInvariant()),
            Status = InvoiceStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Вспомогательная фабрика для счетов на пополнение: создаёт Draft с <c>InvoicePurpose.Topup</c>
    /// и сразу добавляет одну строку <see cref="InvoiceLineItemKind.Adjustment"/>, чтобы
    /// <see cref="SubtotalAmount"/> был задан до выставления счёта.
    /// </summary>
    public static Invoice CreateTopupDraft(
        string tenantId,
        string invoiceNumber,
        int periodYear,
        int periodMonth,
        string currency,
        decimal amount,
        string lineItemDescription)
    {
        var invoice = CreateDraft(tenantId, invoiceNumber, periodYear, periodMonth, currency,
            InvoicePurpose.Topup, periodStartUtc: null, periodEndUtc: null);
        invoice.AddLineItem(InvoiceLineItemKind.Adjustment, lineItemDescription, 1m, amount);
        return invoice;
    }

    public InvoiceLineItem AddLineItem(InvoiceLineItemKind kind, string description, decimal quantity, decimal unitPrice)
    {
        RequireStatus(InvoiceStatus.Draft);
        var line = InvoiceLineItem.Create(Id, kind, description, quantity, unitPrice, Currency);
        _lineItems.Add(line);
        RecalculateTotals();
        return line;
    }

    public void Issue(DateTime? dueAtUtc = null)
    {
        RequireStatus(InvoiceStatus.Draft);
        Status = InvoiceStatus.Issued;
        IssuedAtUtc = DateTime.UtcNow;
        DueAtUtc = dueAtUtc is null
            ? IssuedAtUtc.Value.AddDays(14)
            : DateTime.SpecifyKind(dueAtUtc.Value, DateTimeKind.Utc);
    }

    public void MarkPaid()
    {
        if (Status is InvoiceStatus.Paid)
        {
            return;
        }
        if (Status is not InvoiceStatus.Issued)
        {
            throw new InvalidOperationException($"Невозможно отметить счёт оплаченным из статуса {Status}.");
        }
        Status = InvoiceStatus.Paid;
        PaidAtUtc = DateTime.UtcNow;
    }

    public void Void(string? reason = null)
    {
        if (Status is InvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Оплаченные счета нельзя аннулировать.");
        }
        if (Status is InvoiceStatus.Void)
        {
            // Идемпотентно: повторное аннулирование не должно заново проставлять VoidedAtUtc
            // или повторно добавлять причину.
            return;
        }
        Status = InvoiceStatus.Void;
        VoidedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Notes = string.IsNullOrWhiteSpace(Notes) ? reason : $"{Notes}; Аннулирован: {reason}";
        }
    }

    public void SetNotes(string? notes)
    {
        Notes = notes;
    }

    private void RequireStatus(InvoiceStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Операция требует статус счёта {expected}, но текущий статус {Status}.");
        }
    }

    private void RecalculateTotals()
    {
        SubtotalAmount = _lineItems.Aggregate(
            Money.Zero(SubtotalAmount.Currency),
            (acc, l) => acc.Add(l.Amount));
    }
}
