using EDV.Framework.Shared.Quota;

namespace EDV.Framework.Shared.Multitenancy;

public interface IAppTenantInfo
{
    string? ConnectionString { get; set; }

    /// <summary>Название тарифа, используемое для определения квот по умолчанию 
    /// (возвращается к <c>QuotaOptions.DefaultPlan</c>, если значение null).</summary>
    string? Plan { get; set; }

    /// <summary>Индивидуальные переопределения квот для арендатора. Если ресурс присутствует здесь, 
    /// его значение имеет приоритет над настройками тарифа по умолчанию.</summary>
    Dictionary<QuotaResource, long> QuotaLimits { get; }
}