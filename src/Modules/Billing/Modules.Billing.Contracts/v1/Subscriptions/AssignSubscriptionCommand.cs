using Mediator;

namespace EDV.Modules.Billing.Contracts.v1.Subscriptions;

/// <summary>
/// Административная команда для назначения тенанту тарифа, начиная с текущего момента. Если у тенанта
/// уже есть активная подписка, она будет отменена в этот момент и заменена новой.
/// </summary>
public sealed record AssignSubscriptionCommand(string TenantId, string PlanKey) : ICommand<Guid>;
