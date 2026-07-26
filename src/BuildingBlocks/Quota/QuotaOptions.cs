using EDV.Framework.Shared.Quota;

namespace EDV.Framework.Quota;

/// <summary>
/// Каталог тарифных планов для квот. Арендаторы ссылаются на тариф по имени через <c>AppTenantInfo.Plan</c>;
/// лимиты, привязанные к этому тарифу, используются, когда у арендатора нет индивидуального переопределения.
/// Собственная карта <c>QuotaLimits</c> арендатора имеет приоритет над настройками тарифа по умолчанию,
/// если она присутствует.
/// </summary>
public sealed class QuotaOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Строка подключения к Redis. Если пусто, вместо неё используется внутрипроцессный сервис квот (подходит
    /// только для разработки/тестов — счётчики внутрипроцессные и не разделяются между экземплярами).
    /// </summary>
    public string? Redis { get; set; }

    public string DefaultPlan { get; set; } = "free";

    /// <summary>Карта: название тарифа → лимиты по каждому ресурсу. Используйте -1 или long.MaxValue для "безлимитного".</summary>
    public Dictionary<string, Dictionary<QuotaResource, long>> Plans { get; } = new();

    /// <summary>
    /// Освобождать ли корневого/платформенного арендатора от проверки квот. По умолчанию true;
    /// операторы платформы не должны ограничиваться счётчиками, представляющими единицы биллинга клиентов.
    /// </summary>
    public bool ExemptRootTenant { get; set; } = true;
}