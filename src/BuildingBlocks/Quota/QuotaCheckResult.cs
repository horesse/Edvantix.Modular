using EDV.Framework.Shared.Quota;

namespace EDV.Framework.Quota;

/// <summary>
/// Результат проверки квоты. <see cref="Allowed"/> равно false, если запрошенное количество
/// превысило бы <see cref="Limit"/>. <see cref="ResetAtUtc"/> указывает, когда сбрасывается
/// счётчик (null для ресурсов на основе датчиков, у которых нет границ периода).
/// </summary>
public sealed record QuotaCheckResult(
    bool Allowed,
    QuotaResource Resource,
    long CurrentUsage,
    long Limit,
    DateTimeOffset? ResetAtUtc)
{
    public static QuotaCheckResult Unlimited(QuotaResource resource, long currentUsage)
        => new(true, resource, currentUsage, long.MaxValue, null);
}