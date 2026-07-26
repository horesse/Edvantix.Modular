namespace EDV.Modules.Multitenancy.Domain;

/// <summary>
/// Журнал дедупликации уведомлений об истечении срока действия. Ежедневное сканирование записывает одну строку
/// на комбинацию (тенант, тип уведомления, период действия), поэтому тенант уведомляется один раз для каждого
/// состояния в рамках окна действия — строка автоматически "перезаряжается", когда при продлении меняется
/// <c>ValidUpto</c>. Хранится в кросс-тенантном <c>TenantDbContext</c> (без фильтрации по тенанту), поэтому
/// фоновое сканирование может читать и писать в неё без контекста тенанта.
/// </summary>
public sealed class TenantExpiryNotice
{
    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = default!;
    public string NoticeType { get; private set; } = default!;
    public DateTime ValidUptoUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private TenantExpiryNotice()
    {
    }

    public static TenantExpiryNotice Record(string tenantId, string noticeType, DateTime validUptoUtc, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(noticeType);

        return new TenantExpiryNotice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            NoticeType = noticeType,
            ValidUptoUtc = DateTime.SpecifyKind(validUptoUtc, DateTimeKind.Utc),
            CreatedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc),
        };
    }
}

/// <summary>Устойчивые строковые ключи для <see cref="TenantExpiryNotice.NoticeType"/>.</summary>
public static class TenantExpiryNoticeTypes
{
    public const string NearingExpiry = "NearingExpiry";
    public const string EnteredGrace = "EnteredGrace";
    public const string Expired = "Expired";
}
