using EDV.Modules.Identity.Contracts.v1.Impersonation;

namespace EDV.Modules.Identity.Contracts.Services;

/// <summary>
/// Источник истины для жизненного цикла разрешений на имперсонализацию (выдано → завершено/отозвано).
/// Основан на сущности EF для сохраняемости + HybridCache для горячего пути IsRevokedOrEndedAsync
/// на каждый запрос, используемого хуком валидации JWT.
/// </summary>
public interface IImpersonationGrantService
{
    Task<ImpersonationGrantDto> CreateAsync(CreateGrantInput input, CancellationToken ct = default);

    Task<ImpersonationGrantDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Пометить как естественно завершённое (оператор нажал Завершить на панели). Не изменяет состояние, если уже терминальное.</summary>
    Task<ImpersonationGrantDto?> MarkEndedByJtiAsync(string jti, CancellationToken ct = default);

    Task<ImpersonationGrantDto> RevokeAsync(
        Guid id,
        string revokedByUserId,
        string? revokedByUserName,
        string? reason,
        CancellationToken ct = default);

    /// <summary>
    /// Быстрая проверка, используемая хуком валидации JWT при каждом запросе,
    /// который содержит claim act_sub. Возвращает true, когда разрешение отозвано,
    /// явно завершено или естественно истекло (защита от повторного использования идентификатора токена).
    /// </summary>
    Task<bool> IsRevokedOrEndedAsync(string jti, CancellationToken ct = default);

    Task<IReadOnlyList<ImpersonationGrantDto>> ListAsync(
        ImpersonationGrantStatus? status,
        string? impersonatedTenantId,
        string? actorUserId,
        int take,
        CancellationToken ct = default);
}

public sealed record CreateGrantInput(
    string Jti,
    string ActorUserId,
    string? ActorUserName,
    string ActorTenantId,
    string ImpersonatedUserId,
    string? ImpersonatedUserName,
    string ImpersonatedTenantId,
    string Reason,
    DateTime StartedAtUtc,
    DateTime ExpiresAtUtc,
    string? ClientId,
    string? IpAddress,
    string? UserAgent);