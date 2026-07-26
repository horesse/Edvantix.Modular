using Mediator;

namespace EDV.Modules.Billing.Contracts.v1.Wallets;

/// <summary>
/// Команда оператора — отклоняет запрос на пополнение в статусе Pending. Возвращает идентификатор запроса.
/// </summary>
public sealed record RejectTopupRequestCommand(Guid Id, string? Reason) : ICommand<Guid>;
