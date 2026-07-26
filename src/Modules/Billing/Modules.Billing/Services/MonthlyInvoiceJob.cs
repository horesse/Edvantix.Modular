using Microsoft.Extensions.Logging;

namespace EDV.Modules.Billing.Services;

/// <summary>
/// Периодическая задача Hangfire, формирующая черновики счетов за предыдущий расчётный период.
/// Запланирована на выполнение вскоре после полуночи UTC 1-го числа каждого месяца, чтобы на момент
/// снятия снимка все счётчики использования за предыдущий период ещё присутствовали в Redis
/// (TTL не истёк).
/// </summary>
public sealed class MonthlyInvoiceJob
{
    private readonly IBillingService _billing;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MonthlyInvoiceJob> _logger;

    public MonthlyInvoiceJob(IBillingService billing, TimeProvider timeProvider, ILogger<MonthlyInvoiceJob> logger)
    {
        _billing = billing;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var previous = _timeProvider.GetUtcNow().UtcDateTime.AddMonths(-1);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[Billing] MonthlyInvoiceJob формирует счета за период {Year}-{Month:00}",
                previous.Year, previous.Month);
        }

        var count = await _billing.GenerateInvoicesForAllTenantsAsync(previous.Year, previous.Month, cancellationToken).ConfigureAwait(false);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[Billing] MonthlyInvoiceJob сформировал {Count} черновиков счетов за {Year}-{Month:00}",
                count, previous.Year, previous.Month);
        }
    }
}
