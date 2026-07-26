using EDV.Framework.Shared.Persistence;
using EDV.Modules.Billing.Contracts.Dtos;
using Mediator;

namespace EDV.Modules.Billing.Contracts.v1.Wallets;

/// <summary>
/// Запрос оператора — возвращает список запросов на пополнение по всем тенантам с опциональными фильтрами.
/// Root-вызывающие получают представление по всем тенантам (опционально сужаемое через
/// <paramref name="TenantId"/>); вызывающие без root-прав автоматически ограничиваются своим тенантом.
/// </summary>
public sealed record GetTopupRequestsQuery(
    string? TenantId = null,
    TopupRequestStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<TopupRequestDto>>;
