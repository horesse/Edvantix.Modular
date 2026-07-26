using EDV.Modules.Billing.Domain;

namespace EDV.Modules.Billing.Services;

/// <summary>
/// Основной процесс биллинга: снимок использования, расчёт цены, выставление счёта, отслеживание
/// статуса оплаты. Интеграция с платёжным процессором намеренно не входит в область — счета
/// отмечаются оплаченными вручную.
/// </summary>
public interface IBillingService
{
    /// <summary>
    /// Возвращает кошелёк для <paramref name="tenantId"/>, создавая его, если он ещё не существует.
    /// Кошелёк — это единственный журнал баланса на тенанта для предоплаченного кредита (например, WhatsApp).
    /// </summary>
    Task<Wallet> GetOrCreateWalletAsync(string tenantId, string currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт и выставляет счёт с назначением Topup для ожидающего <see cref="TopupRequest"/>,
    /// вызывает <c>InvoiceIssuedIntegrationEvent</c>, вызывает <c>request.MarkInvoiced</c> и сохраняет —
    /// всё в рамках одной единицы работы.
    /// </summary>
    Task<Invoice> CreateTopupInvoiceAsync(string tenantId, Guid topupRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Формирует черновик счёта для тенанта/периода на основе снимка использования и расчёта цены
    /// по активному тарифу подписки тенанта. Возвращает null, если у тенанта нет активной подписки
    /// или счёт за этот период уже существует.
    /// </summary>
    Task<Invoice?> GenerateInvoiceForPeriodAsync(
        string tenantId,
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Формирует черновики счетов для каждого тенанта с активной подпиской за указанный период.
    /// Возвращает количество созданных счетов (уже существующие за этот период счета пропускаются).
    /// </summary>
    Task<int> GenerateInvoicesForAllTenantsAsync(
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт и выставляет счёт с назначением Subscription за один срок тарифа (базовая плата за срок).
    /// Вызывается при подписке или продлении тенанта. Возвращает null для бесплатных тарифов с нулевой
    /// ценой (счёт не создаётся). Идемпотентно: возвращает существующий счёт, если он уже создан за этот срок.
    /// </summary>
    Task<Invoice?> CreateSubscriptionInvoiceAsync(
        string tenantId,
        Guid planId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default);

    Task IssueInvoiceAsync(Guid invoiceId, DateTime? dueAtUtc, CancellationToken cancellationToken = default);

    Task MarkInvoicePaidAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task VoidInvoiceAsync(Guid invoiceId, string? reason, CancellationToken cancellationToken = default);
}
