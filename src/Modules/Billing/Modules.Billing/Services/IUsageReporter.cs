using EDV.Modules.Billing.Domain;

namespace EDV.Modules.Billing.Services;

/// <summary>
/// Фиксирует снимок использования тенанта за расчётный период, читая данные из <c>IQuotaService</c>
/// и определяя действующий лимит. Снимки сохраняются в таблицу <c>UsageSnapshots</c>, чтобы расчёты
/// по счетам оставались воспроизводимыми, даже если тариф впоследствии изменится.
/// </summary>
public interface IUsageReporter
{
    /// <summary>
    /// Фиксирует один <see cref="UsageSnapshot"/> на каждый <c>QuotaResource</c> для указанного
    /// тенанта/периода. Идемпотентно: если снимок для (тенант, период, ресурс) уже существует,
    /// возвращается существующая запись вместо создания новой.
    /// </summary>
    Task<IReadOnlyList<UsageSnapshot>> CaptureForPeriodAsync(
        string tenantId,
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default);
}
