using Mediator;

namespace EDV.Modules.Billing.Contracts.v1.Wallets;

/// <summary>
/// Команда оператора — одобряет запрос на пополнение в статусе Pending, создаёт и выставляет
/// счёт с назначением Topup, переводя запрос в статус Invoiced. Возвращает идентификатор созданного счёта.
/// </summary>
public sealed record ApproveTopupRequestCommand(Guid Id, string? Note) : ICommand<Guid>;
