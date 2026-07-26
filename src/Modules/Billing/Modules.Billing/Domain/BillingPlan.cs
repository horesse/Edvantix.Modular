using EDV.Framework.Core.Domain;
using EDV.Framework.Shared.Quota;
using EDV.Modules.Billing.Contracts;

namespace EDV.Modules.Billing.Domain;

/// <summary>
/// Тарифицируемая сторона тарифа тенанта. Ключ тарифа совпадает с ключом, используемым конфигурацией
/// квот, поэтому тариф с именем "pro" в QuotaOptions.Plans соответствует BillingPlan с Key "pro".
/// Лимиты берутся из QuotaOptions; цены и ставки перерасхода — отсюда.
///
/// <see cref="IGlobalEntity"/>: тарифы — это записи каталога уровня платформы, а НЕ данные конкретного
/// тенанта. Каждый тенант подписывается на один из этих общих тарифов.
/// </summary>
public sealed class BillingPlan : BaseEntity<Guid>, IGlobalEntity
{
    private readonly Dictionary<QuotaResource, decimal> _overageRates = new();

    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public Money MonthlyBasePrice { get; private set; } = default!;
    public string Currency => MonthlyBasePrice.Currency;
    public PlanInterval Interval { get; private set; } = PlanInterval.Monthly;

    /// <summary>
    /// Фиксированная цена, взимаемая за годовой срок. Имеет смысл только когда <see cref="Interval"/>
    /// равен <see cref="PlanInterval.Yearly"/>; <c>null</c> означает запасной вариант в виде
    /// двенадцатикратной базовой месячной цены, чтобы годовой тариф можно было настроить без
    /// повторного указания скидки.
    /// </summary>
    public Money? AnnualPrice { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyDictionary<QuotaResource, decimal> OverageRates => _overageRates;

    private BillingPlan() { }

    public static BillingPlan Create(
        string key,
        string name,
        string currency,
        decimal monthlyBasePrice,
        IReadOnlyDictionary<QuotaResource, decimal>? overageRates = null,
        PlanInterval interval = PlanInterval.Monthly,
        decimal? annualPrice = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (monthlyBasePrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyBasePrice), "Цена не может быть отрицательной.");
        }
        if (annualPrice is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(annualPrice), "Годовая цена не может быть отрицательной.");
        }

        var plan = new BillingPlan
        {
            Id = Guid.CreateVersion7(),
#pragma warning disable CA1308 // Plan keys are canonical slugs stored lowercase (not security-sensitive)
            Key = key.ToLowerInvariant(),
#pragma warning restore CA1308
            Name = name,
            MonthlyBasePrice = new Money(monthlyBasePrice, currency),
            Interval = interval,
            AnnualPrice = annualPrice is { } a ? new Money(a, currency) : null,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (overageRates is not null)
        {
            foreach (var (res, rate) in overageRates)
            {
                plan._overageRates[res] = rate;
            }
        }

        return plan;
    }

    public void Update(
        string name,
        decimal monthlyBasePrice,
        IReadOnlyDictionary<QuotaResource, decimal>? overageRates,
        PlanInterval interval = PlanInterval.Monthly,
        decimal? annualPrice = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (monthlyBasePrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyBasePrice), "Цена не может быть отрицательной.");
        }
        if (annualPrice is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(annualPrice), "Годовая цена не может быть отрицательной.");
        }

        Name = name;
        MonthlyBasePrice = new Money(monthlyBasePrice, Currency);
        Interval = interval;
        AnnualPrice = annualPrice is { } a ? new Money(a, Currency) : null;
        _overageRates.Clear();
        if (overageRates is not null)
        {
            foreach (var (res, rate) in overageRates)
            {
                _overageRates[res] = rate;
            }
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public decimal GetOverageRate(QuotaResource resource) =>
        _overageRates.TryGetValue(resource, out var rate) ? rate : 0m;

    /// <summary>Количество месяцев, которое покрывает расчётный интервал тарифа (1 для месячного, 12 для годового).</summary>
    public int TermMonths => Interval == PlanInterval.Yearly ? 12 : 1;

    /// <summary>
    /// Цена, взимаемая за один расчётный срок: базовая месячная цена для месячных тарифов
    /// либо годовая цена (с запасным вариантом в виде двенадцати месяцев) для годовых тарифов.
    /// </summary>
    public Money TermPrice =>
        Interval == PlanInterval.Yearly
            ? AnnualPrice ?? MonthlyBasePrice.Multiply(12m)
            : MonthlyBasePrice;
}
