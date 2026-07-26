using EDV.Framework.Eventing.Abstractions;

namespace EDV.Modules.Billing.Contracts.Events;

/// <summary>
/// Возникает, когда счёт переходит в статус Issued и становится действительным к оплате (например,
/// счёт по подписке, формируемый при создании или продлении тенанта). Подписчики уведомляют тенант
/// о наступлении срока оплаты.
/// </summary>
public sealed record InvoiceIssuedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid InvoiceId,
    string InvoiceNumber,
    decimal Amount,
    string Currency,
    DateTime? DueAtUtc,
    int PeriodYear,
    int PeriodMonth)
    : IIntegrationEvent;
