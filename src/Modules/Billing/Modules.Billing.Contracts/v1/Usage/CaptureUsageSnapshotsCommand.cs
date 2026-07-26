using EDV.Modules.Billing.Contracts.Dtos;
using Mediator;

namespace EDV.Modules.Billing.Contracts.v1.Usage;

/// <summary>
/// Эксплуатационная команда, которая фиксирует один снимок использования на каждый <c>QuotaResource</c>
/// для пары тенант + период. Оборачивает <c>IUsageReporter.CaptureForPeriodAsync</c>. Идемпотентна:
/// повторный запуск для той же пары (тенант, период) возвращает уже существующие снимки без создания дублей.
/// </summary>
public sealed record CaptureUsageSnapshotsCommand(
    string TenantId,
    int PeriodYear,
    int PeriodMonth) : ICommand<IReadOnlyList<UsageSnapshotDto>>;
