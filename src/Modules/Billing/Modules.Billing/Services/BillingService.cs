using EDV.Framework.Core.Exceptions;
using EDV.Framework.Eventing.Abstractions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Contracts;
using EDV.Modules.Billing.Contracts.Events;
using EDV.Modules.Billing.Data;
using EDV.Modules.Billing.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Billing.Services;

public sealed class BillingService : IBillingService
{
    private readonly BillingDbContext _db;
    private readonly IUsageReporter _usageReporter;
    private readonly IMultiTenantStore<AppTenantInfo> _tenantStore;
    private readonly IMultiTenantContextAccessor<AppTenantInfo> _tenantAccessor;
    private readonly IEventBus _eventBus;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        BillingDbContext db,
        IUsageReporter usageReporter,
        IMultiTenantStore<AppTenantInfo> tenantStore,
        IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
        IEventBus eventBus,
        TimeProvider timeProvider,
        ILogger<BillingService> logger)
    {
        _db = db;
        _usageReporter = usageReporter;
        _tenantStore = tenantStore;
        _tenantAccessor = tenantAccessor;
        _eventBus = eventBus;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Invoice?> GenerateInvoiceForPeriodAsync(
        string tenantId,
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        // Ограничиваем проверку идемпотентности назначением Purpose==Usage: счёт с назначением
        // Subscription вполне может относиться к тому же месяцу, и без этого фильтра мы бы нашли его
        // и пропустили счёт за использование/перерасход (неучтённый перерасход).
        var existing = await _db.Invoices
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.PeriodYear == periodYear && i.PeriodMonth == periodMonth
                && i.Purpose == InvoicePurpose.Usage, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("[Billing] счёт за использование уже существует для тенанта {TenantId} за период {Year}-{Month:00}, пропускаем",
                    tenantId, periodYear, periodMonth);
            }
            return existing;
        }

        var subscription = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Status == SubscriptionStatus.Active, cancellationToken)
            .ConfigureAwait(false);
        if (subscription is null)
        {
            _logger.LogWarning("[Billing] у тенанта {TenantId} нет активной подписки, счёт пропущен", tenantId);
            return null;
        }

        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == subscription.PlanId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Тариф {subscription.PlanId} не найден для тенанта {tenantId}.");

        var snapshots = await _usageReporter.CaptureForPeriodAsync(tenantId, periodYear, periodMonth, cancellationToken).ConfigureAwait(false);

        // Счета за использование тарифицируют только учитываемый перерасход. Базовая плата тарифа
        // тарифицируется счётом по подписке при создании/продлении тенанта (см. CreateSubscriptionInvoiceAsync),
        // поэтому здесь она намеренно НЕ добавляется — иначе месячные тарифы тарифицировались бы дважды.
        var invoiceNumber = BuildUsageInvoiceNumber(tenantId, periodYear, periodMonth);
        var invoice = Invoice.CreateDraft(tenantId, invoiceNumber, periodYear, periodMonth, plan.Currency,
            InvoicePurpose.Usage, periodStartUtc: null, periodEndUtc: null);

        foreach (var snap in snapshots)
        {
            if (snap.Overage <= 0)
            {
                continue;
            }
            var rate = plan.GetOverageRate(snap.Resource);
            if (rate <= 0)
            {
                continue;
            }
            var line = invoice.AddLineItem(
                InvoiceLineItemKind.Overage,
                $"Перерасход {snap.Resource} ({snap.Overage} ед.)",
                snap.Overage,
                rate);
            line.AttachResource(snap.Resource);
        }

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[Billing] сформирован черновик счёта {InvoiceNumber} для тенанта {TenantId} за период {Year}-{Month:00}, сумма={Total} {Currency}",
                invoice.InvoiceNumber, tenantId, periodYear, periodMonth, invoice.SubtotalAmount.Amount, invoice.Currency);
        }
        return invoice;
    }

    public async Task<int> GenerateInvoicesForAllTenantsAsync(
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default)
    {
        var tenants = await _tenantStore.GetAllAsync().ConfigureAwait(false);
        var activeTenantIds = tenants.Where(t => t.IsActive).Select(t => t.Id).ToList();
        var subscribedTenantIds = await _db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active && activeTenantIds.Contains(s.TenantId))
            .Select(s => s.TenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var alreadyInvoiced = await _db.Invoices
            .Where(i => i.PeriodYear == periodYear && i.PeriodMonth == periodMonth
                && i.Purpose == InvoicePurpose.Usage && subscribedTenantIds.Contains(i.TenantId))
            .Select(i => i.TenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var toGenerate = subscribedTenantIds.Except(alreadyInvoiced, StringComparer.Ordinal).ToList();

        var generated = 0;
        foreach (var tenantId in toGenerate)
        {
            try
            {
                var inv = await GenerateInvoiceForPeriodAsync(tenantId, periodYear, periodMonth, cancellationToken).ConfigureAwait(false);
                if (inv is not null)
                {
                    generated++;
                }
            }
#pragma warning disable CA1031 // One tenant's failure must not block the others
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "[Billing] не удалось сформировать счёт для тенанта {TenantId} за период {Year}-{Month:00}",
                    tenantId, periodYear, periodMonth);
            }
        }
        return generated;
    }

    public async Task IssueInvoiceAsync(Guid invoiceId, DateTime? dueAtUtc, CancellationToken cancellationToken = default)
    {
        var invoice = await LoadInvoiceAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        invoice.Issue(dueAtUtc);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Wallet> GetOrCreateWalletAsync(string tenantId, string currency, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var wallet = await _db.Wallets
            .Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (wallet is null)
        {
            wallet = Wallet.Create(tenantId, currency);
            _db.Wallets.Add(wallet);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        return wallet;
    }

    public async Task<Invoice> CreateTopupInvoiceAsync(string tenantId, Guid topupRequestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var request = await _db.TopupRequests
            .FirstOrDefaultAsync(r => r.Id == topupRequestId && r.TenantId == tenantId && r.Status == TopupRequestStatus.Pending, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Запрос на пополнение {topupRequestId} не найден или не находится в ожидании.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var invoiceNumber = BuildTopupInvoiceNumber(tenantId, now, topupRequestId);

        var invoice = Invoice.CreateTopupDraft(
            tenantId,
            invoiceNumber,
            now.Year,
            now.Month,
            request.Amount.Currency,
            request.Amount.Amount,
            $"Пополнение кошелька WhatsApp ({request.Amount.Amount:0.##} {request.Amount.Currency})");

        invoice.Issue();
        _db.Invoices.Add(invoice);
        request.MarkInvoiced(invoice.Id, request.Note);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "[Billing] выставлен счёт на пополнение {InvoiceNumber} для тенанта {TenantId}, сумма={Amount} {Currency}",
                invoice.InvoiceNumber, tenantId, invoice.SubtotalAmount.Amount, invoice.Currency);
        }

        await _eventBus.PublishAsync(new InvoiceIssuedIntegrationEvent(
            Id: Guid.NewGuid(),
            OccurredOnUtc: now,
            TenantId: tenantId,
            CorrelationId: Guid.NewGuid().ToString(),
            Source: "Billing",
            InvoiceId: invoice.Id,
            InvoiceNumber: invoice.InvoiceNumber,
            Amount: invoice.SubtotalAmount.Amount,
            Currency: invoice.Currency,
            DueAtUtc: invoice.DueAtUtc,
            PeriodYear: invoice.PeriodYear,
            PeriodMonth: invoice.PeriodMonth), cancellationToken).ConfigureAwait(false);

        return invoice;
    }

    public async Task MarkInvoicePaidAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await LoadInvoiceAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        invoice.MarkPaid();

        // Когда счёт на пополнение оплачен, зачисляем средства на кошелёк тенанта и завершаем запрос —
        // всё в рамках одного SaveChanges, чтобы зачисление и смена статуса были атомарны.
        if (invoice.Purpose == InvoicePurpose.Topup)
        {
            var topupRequest = await _db.TopupRequests
                .FirstOrDefaultAsync(r => r.InvoiceId == invoice.Id, cancellationToken)
                .ConfigureAwait(false);

            if (topupRequest is { Status: TopupRequestStatus.Invoiced })
            {
                var wallet = await _db.Wallets
                    .FirstOrDefaultAsync(w => w.TenantId == invoice.TenantId, cancellationToken)
                    .ConfigureAwait(false);

                if (wallet is null)
                {
                    wallet = Wallet.Create(invoice.TenantId, invoice.Currency);
                    _db.Wallets.Add(wallet);
                }

                wallet.Credit(
                    invoice.SubtotalAmount.Amount,
                    WalletTransactionKind.Topup,
                    "Пополнение кошелька WhatsApp",
                    topupRequest.Id.ToString());

                topupRequest.MarkCompleted();
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task VoidInvoiceAsync(Guid invoiceId, string? reason, CancellationToken cancellationToken = default)
    {
        var invoice = await LoadInvoiceAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        invoice.Void(reason);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // Issue/MarkPaid/Void загружают счёт здесь. BillingDbContext не фильтруется по тенанту, поэтому
    // ограничиваем область вызывающим: root может изменять любой счёт; вызывающий в контексте тенанта
    // ограничен своим (чужой идентификатор → 404, изменить нельзя).
    private async Task<Invoice> LoadInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var callerTenantId = _tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Требуется контекст тенанта.");
        var isRoot = callerTenantId == MultitenancyConstants.Root.Id;

        return await _db.Invoices
            .FirstOrDefaultAsync(i => i.Id == invoiceId && (isRoot || i.TenantId == callerTenantId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Счёт {invoiceId} не найден.");
    }

    public async Task<Invoice?> CreateSubscriptionInvoiceAsync(
        string tenantId,
        Guid planId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == planId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Тариф {planId} не найден для тенанта {tenantId}.");

        var termPrice = plan.TermPrice;
        if (termPrice.Amount <= 0m)
        {
            // Бесплатный/пробный тариф — срок действия всё равно устанавливается, но тарифицировать нечего.
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("[Billing] цена срока тарифа {PlanKey} равна нулю для тенанта {TenantId}, счёт по подписке не создаётся", plan.Key, tenantId);
            }
            return null;
        }

        var periodStart = DateTime.SpecifyKind(periodStartUtc, DateTimeKind.Utc);
        var periodEnd = DateTime.SpecifyKind(periodEndUtc, DateTimeKind.Utc);
        var invoiceNumber = BuildSubscriptionInvoiceNumber(tenantId, periodStart);

        // Идемпотентность: повторная доставка события подписки/продления не должна выставлять счёт за срок дважды.
        var existing = await _db.Invoices
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.InvoiceNumber == invoiceNumber, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var invoice = Invoice.CreateDraft(tenantId, invoiceNumber, periodStart.Year, periodStart.Month,
            plan.Currency, InvoicePurpose.Subscription, periodStart, periodEnd);
        invoice.AddLineItem(
            InvoiceLineItemKind.BaseFee,
            $"{plan.Name} — подписка {plan.Interval} ({periodStart:yyyy-MM-dd} — {periodEnd:yyyy-MM-dd})",
            1m,
            termPrice.Amount);
        invoice.Issue();

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[Billing] выставлен счёт по подписке {InvoiceNumber} для тенанта {TenantId}, сумма={Total} {Currency}",
                invoice.InvoiceNumber, tenantId, invoice.SubtotalAmount.Amount, invoice.Currency);
        }

        // Уведомляем (например, письмом тенанту), что выставлен реальный счёт. Срабатывает только для
        // вновь созданных счетов — при повторной доставке события идемпотентный ранний возврат выше
        // пропускает этот шаг.
        await _eventBus.PublishAsync(new InvoiceIssuedIntegrationEvent(
            Id: Guid.NewGuid(),
            OccurredOnUtc: _timeProvider.GetUtcNow().UtcDateTime,
            TenantId: tenantId,
            CorrelationId: Guid.NewGuid().ToString(),
            Source: "Billing",
            InvoiceId: invoice.Id,
            InvoiceNumber: invoice.InvoiceNumber,
            Amount: invoice.SubtotalAmount.Amount,
            Currency: invoice.Currency,
            DueAtUtc: invoice.DueAtUtc,
            PeriodYear: invoice.PeriodYear,
            PeriodMonth: invoice.PeriodMonth), cancellationToken).ConfigureAwait(false);

        return invoice;
    }

    private static string BuildUsageInvoiceNumber(string tenantId, int periodYear, int periodMonth) =>
        $"USG-{periodYear}{periodMonth:00}-{TenantToken(tenantId)}";

    private static string BuildSubscriptionInvoiceNumber(string tenantId, DateTime periodStartUtc) =>
        $"SUB-{periodStartUtc:yyyyMM}-{TenantToken(tenantId)}";

    /// <summary>
    /// Формирует защищённый от коллизий номер счёта для пополнения.
    /// Формат: <c>TOP-{yyyyMM}-{tenantToken}-{requestSuffix}</c>,
    /// где <c>requestSuffix</c> — 8 шестнадцатеричных символов из последних 4 байт <paramref name="topupRequestId"/>.
    /// У каждого <see cref="TopupRequest"/> уникальный <see cref="Guid"/>, поэтому два пополнения одного
    /// тенанта в одном месяце получают различные номера и никогда не сталкиваются на уникальном индексе InvoiceNumber.
    /// </summary>
    private static string BuildTopupInvoiceNumber(string tenantId, DateTime now, Guid topupRequestId)
    {
        var suffix = Convert.ToHexString(topupRequestId.ToByteArray(), 12, 4);
        return $"TOP-{now:yyyyMM}-{TenantToken(tenantId)}-{suffix}";
    }

    // Стабильный, устойчивый к коллизиям токен из полного идентификатора тенанта; наивное усечение
    // по префиксу приводило бы к коллизиям у тенантов с общим префиксом и конфликтам на уникальном
    // индексе InvoiceNumber.
    private static string TenantToken(string tenantId)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(tenantId));
        return Convert.ToHexString(hash, 0, 6);
    }
}
