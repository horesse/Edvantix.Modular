using EDV.Framework.Shared.Persistence;
using EDV.Modules.Billing.Contracts.Dtos;
using Mediator;

namespace EDV.Modules.Billing.Contracts.v1.Invoices;

/// <summary>
/// Административный запрос — возвращает список счетов по всем тенантам с опциональными фильтрами.
/// Вызывающим в контексте конкретного тенанта следует использовать <c>GetMyInvoicesQuery</c>,
/// чтобы не допустить утечки данных между тенантами.
/// </summary>
public sealed record GetInvoicesQuery(
    string? TenantId = null,
    InvoiceStatus? Status = null,
    int? PeriodYear = null,
    int? PeriodMonth = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<InvoiceDto>>;
