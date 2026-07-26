using EDV.Modules.Billing.Contracts.Dtos;
using Mediator;

namespace EDV.Modules.Billing.Contracts.v1.Subscriptions;

/// <summary>
/// Возвращает текущую активную подписку указанного тенанта. Вызывающие в контексте тенанта обычно
/// передают null, чтобы получить свою собственную подписку; администраторы могут передать
/// идентификатор любого тенанта.
/// </summary>
public sealed record GetSubscriptionQuery(string? TenantId = null) : IQuery<SubscriptionDto?>;
