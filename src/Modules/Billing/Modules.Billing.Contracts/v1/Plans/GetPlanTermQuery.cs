using Mediator;

namespace EDV.Modules.Billing.Contracts.v1.Plans;

/// <summary>
/// Считывает расчётный срок тарифа, чтобы другой модуль (Multitenancy) мог вычислить окно
/// действия тенанта без обращения к рантайму Billing. Диспетчеризуется через Mediator
/// поверх границы модулей.
/// </summary>
public sealed record GetPlanTermQuery(string PlanKey) : IQuery<PlanTermResponse>;

public sealed record PlanTermResponse(
    Guid PlanId,
    string Key,
    string Name,
    PlanInterval Interval,
    int TermMonths,
    decimal UnitPrice,
    string Currency);
