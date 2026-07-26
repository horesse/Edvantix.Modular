using EDV.Framework.Core.Domain;
using EDV.Framework.Shared.Quota;

namespace EDV.Modules.Billing.Domain;

/// <summary>
/// Зафиксированная запись об использовании тенантом одного ресурса за расчётный период. Формируется
/// на основе <c>IQuotaService.GetCurrentAsync</c> в момент закрытия периода — мы фиксируем и Used, и
/// Limit, чтобы расчёты по счёту и любой последующий аудит оставались воспроизводимыми, даже если
/// тариф впоследствии изменится.
/// </summary>
public sealed class UsageSnapshot : BaseEntity<Guid>
{
    public string TenantId { get; private set; } = default!;
    public int PeriodYear { get; private set; }
    public int PeriodMonth { get; private set; }
    public QuotaResource Resource { get; private set; }
    public long UsedUnits { get; private set; }
    public long LimitUnits { get; private set; }
    public DateTime CapturedAtUtc { get; private set; }

    private UsageSnapshot() { }

    public static UsageSnapshot Capture(
        string tenantId,
        int periodYear,
        int periodMonth,
        QuotaResource resource,
        long usedUnits,
        long limitUnits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (periodYear is < 2000 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(periodYear));
        }
        if (periodMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(periodMonth));
        }

        return new UsageSnapshot
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            PeriodYear = periodYear,
            PeriodMonth = periodMonth,
            Resource = resource,
            UsedUnits = usedUnits,
            LimitUnits = limitUnits,
            CapturedAtUtc = DateTime.UtcNow
        };
    }

    public long Overage => UsedUnits > LimitUnits ? UsedUnits - LimitUnits : 0;
}
