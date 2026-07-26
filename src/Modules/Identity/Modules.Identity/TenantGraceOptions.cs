namespace EDV.Modules.Identity;

/// <summary>
/// Представление льготного периода биллинга арендатора со стороны входа (секция конфигурации <c>"Billing"</c>).
/// Арендатор с истёкшей подпиской всё ещё может аутентифицироваться до <c>ValidUpto + GracePeriodDays</c>.
/// </summary>
public sealed class TenantGraceOptions
{
    public const string SectionName = "Billing";

    public int GracePeriodDays { get; set; } = 7;
}
