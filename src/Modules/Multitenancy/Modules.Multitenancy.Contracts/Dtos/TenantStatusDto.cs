namespace EDV.Modules.Multitenancy.Contracts.Dtos;

public sealed class TenantStatusDto
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public bool IsActive { get; init; }
    public DateTime ValidUpto { get; init; }
    public bool HasConnectionString { get; init; }
    public string AdminEmail { get; init; } = default!;
    public string? Issuer { get; init; }

    /// <summary>Ключ текущего тарифного плана тенанта (определяет квоты и подписку).</summary>
    public string? Plan { get; init; }

    /// <summary>Производное состояние жизненного цикла: "Active", "InGrace" или "Expired".</summary>
    public string ExpiryState { get; init; } = "Active";

    /// <summary>Момент времени, после которого просроченный тенант жёстко блокируется (ValidUpto + льготный период).</summary>
    public DateTime GraceEndsUtc { get; init; }
}