using Mediator;

namespace EDV.Modules.Multitenancy.Contracts.v1.RenewTenant;

/// <summary>
/// Продлевает тенанта ещё на один срок плана. Если <see cref="PlanKey"/> равен null, продлевается
/// текущий план; если он отличается, тенант переключается на новый план начиная с продления.
/// </summary>
public sealed record RenewTenantCommand(string TenantId, string? PlanKey = null)
    : ICommand<RenewTenantCommandResponse>;
