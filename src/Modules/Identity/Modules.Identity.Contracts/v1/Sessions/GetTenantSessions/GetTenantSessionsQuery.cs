using EDV.Framework.Shared.Persistence;
using EDV.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Sessions.GetTenantSessions;

/// <summary>
/// Возвращает все сессии в текущем арендаторе, с пагинацией и опциональной
/// фильтрацией. Используется для административной поверхности "системные сессии" —
/// отличается от запроса GetUserSessions для конкретного пользователя, потому что
/// требует другого разрешения и другой структуры (пагинированный список vs плоский список).
/// </summary>
public sealed record GetTenantSessionsQuery : IQuery<PagedResponse<UserSessionDto>>
{
    /// <summary>Если true, включает истёкшие/отозванные сессии. По умолчанию только активные.</summary>
    public bool IncludeInactive { get; init; }

    /// <summary>Необязательный фильтр подстроки по имени пользователя, email или IP-адресу.</summary>
    public string? Search { get; init; }

    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}