using Mediator;

namespace EDV.Modules.Billing.Contracts.v1.Invoices;

/// <summary>
/// Запускаемая администратором генерация счетов за указанный расчётный период по всем активным тенантам.
/// Идемпотентна: при повторном запуске для периода, за который счета уже сформированы, такие тенанты пропускаются.
/// </summary>
public sealed record GenerateInvoicesCommand(int PeriodYear, int PeriodMonth) : ICommand<int>;
