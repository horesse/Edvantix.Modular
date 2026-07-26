namespace EDV.Modules.Multitenancy;

/// <summary>
/// Параметры биллинга жизненного цикла тенанта (секция конфигурации <c>"Billing"</c>): план, на который
/// откатывается тенант при создании без явного указания плана, и сколько времени после <c>ValidUpto</c>
/// тенант продолжает работать, прежде чем будет жёстко заблокирован.
/// </summary>
public sealed class TenantBillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>Ключ плана, назначаемый при вызове CreateTenant без явного указания плана.</summary>
    public string DefaultPlanKey { get; set; } = "free";

    /// <summary>Количество дней после <c>ValidUpto</c>, в течение которых запросы и вход в систему всё ещё выполняются успешно.</summary>
    public int GracePeriodDays { get; set; } = 7;

    /// <summary>За сколько дней до <c>ValidUpto</c> ежедневное сканирование начинает отправлять напоминания о скором истечении срока.</summary>
    public int ExpiryNotificationLeadDays { get; set; } = 7;
}
