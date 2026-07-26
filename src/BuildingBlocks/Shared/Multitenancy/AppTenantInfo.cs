using EDV.Framework.Shared.Quota;
using Finbuckle.MultiTenant.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace EDV.Framework.Shared.Multitenancy;

public class AppTenantInfo : TenantInfo, IAppTenantInfo
{
    // Конструктор без параметров для инструментов/EF.
    [SetsRequiredMembers]
    public AppTenantInfo()
    {
        Id = string.Empty;
        Identifier = string.Empty;
    }

    [SetsRequiredMembers]
    public AppTenantInfo(string id, string identifier, string? name = null)
    {
        Id = id;
        Identifier = identifier;
        Name = name;
    }

    [SetsRequiredMembers]
    public AppTenantInfo(string id, string name, string? connectionString, string adminEmail, string? issuer = null)
        : this(id, id, name)
    {
        ConnectionString = connectionString ?? string.Empty;
        AdminEmail = adminEmail;
        IsActive = true;
        Issuer = issuer;

        // Добавление срока действия по умолчанию — 1 месяц для всех новых арендаторов.
        // Что-то вроде DEMO-периода для арендаторов.
        ValidUpto = TimeProvider.System.GetUtcNow().UtcDateTime.AddMonths(1);
    }

    public string ConnectionString { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime ValidUpto { get; set; }
    public string? Issuer { get; set; }

    /// <summary>Название тарифа, используемое для определения квот по умолчанию (например, "free", "pro"). 
    /// Значение null возвращается к <c>QuotaOptions.DefaultPlan</c>.</summary>
    public string? Plan { get; set; }

    /// <summary>Индивидуальные переопределения квот для арендатора. Сериализуются в JSON хранилищем арендаторов;
    /// по умолчанию пусто.</summary>
    public Dictionary<QuotaResource, long> QuotaLimits { get; set; } = new();

    public void AddValidity(int months) =>
        ValidUpto = ValidUpto.AddMonths(months);

    public void SetValidity(in DateTime validTill)
    {
        var normalized = validTill;
        ValidUpto = ValidUpto < normalized
            ? normalized
            : throw new InvalidOperationException("Подписка не может быть перенесена на более раннюю дату.");
    }

    public void Activate()
    {
        if (Id == MultitenancyConstants.Root.Id)
        {
            throw new InvalidOperationException("Недействительный арендатор");
        }

        IsActive = true;
    }

    public void Deactivate()
    {
        if (Id == MultitenancyConstants.Root.Id)
        {
            throw new InvalidOperationException("Недействительный арендатор");
        }

        IsActive = false;
    }

    string? IAppTenantInfo.ConnectionString
    {
        get => ConnectionString;
        set => ConnectionString = value ?? throw new InvalidOperationException("ConnectionString не может быть null.");
    }
}