using EDV.Framework.Core.Domain;
using EDV.Modules.Billing.Contracts;

namespace EDV.Modules.Billing.Domain;

/// <summary>
/// Связывает тенанта с тарифом на определённом временном окне. В каждый момент времени у тенанта
/// может быть не более одной подписки в статусе Active — при назначении новой подписки предыдущая
/// активная отменяется (Cancelled, EndUtc устанавливается равным StartUtc новой подписки).
/// </summary>
public sealed class Subscription : BaseEntity<Guid>
{
    public string TenantId { get; private set; } = default!;
    public Guid PlanId { get; private set; }
    public DateTime StartUtc { get; private set; }
    public DateTime? EndUtc { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private Subscription() { }

    public static Subscription Create(string tenantId, Guid planId, DateTime startUtc)
        => Create(tenantId, planId, startUtc, endUtc: null);

    public static Subscription Create(string tenantId, Guid planId, DateTime startUtc, DateTime? endUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (planId == Guid.Empty)
        {
            throw new ArgumentException("PlanId обязателен.", nameof(planId));
        }

        return new Subscription
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            PlanId = planId,
            StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
            EndUtc = endUtc is { } e ? DateTime.SpecifyKind(e, DateTimeKind.Utc) : null,
            Status = SubscriptionStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Suspend()
    {
        Status = SubscriptionStatus.Suspended;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        Status = SubscriptionStatus.Active;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Cancel(DateTime endUtc)
    {
        Status = SubscriptionStatus.Cancelled;
        EndUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Продлевает окончание активного срока. Используется при продлении того же тарифа, чтобы
    /// <see cref="EndUtc"/> оставался синхронизирован с ValidUpto тенанта (при смене тарифа подписка
    /// заменяется целиком). Идемпотентно: окончание срока только сдвигается вперёд, поэтому повторная
    /// доставка события продления не имеет эффекта.
    /// </summary>
    public void Extend(DateTime endUtc)
    {
        var newEnd = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);
        if (EndUtc is null || newEnd > EndUtc)
        {
            EndUtc = newEnd;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
