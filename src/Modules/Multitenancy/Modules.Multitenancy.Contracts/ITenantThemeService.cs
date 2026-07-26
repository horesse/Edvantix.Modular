using EDV.Modules.Multitenancy.Contracts.Dtos;

namespace EDV.Modules.Multitenancy.Contracts;

public interface ITenantThemeService
{
    /// <summary>
    /// Возвращает тему для указанного тенанта. При отсутствии темы возвращает тему по умолчанию.
    /// </summary>
    Task<TenantThemeDto> GetThemeAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Возвращает тему для текущего контекста тенанта. При отсутствии темы возвращает тему по умолчанию.
    /// </summary>
    Task<TenantThemeDto> GetCurrentTenantThemeAsync(CancellationToken ct = default);

    /// <summary>
    /// Возвращает тему по умолчанию (заданную корневым тенантом) для новых тенантов.
    /// </summary>
    Task<TenantThemeDto> GetDefaultThemeAsync(CancellationToken ct = default);

    /// <summary>
    /// Обновляет тему для указанного тенанта.
    /// </summary>
    Task UpdateThemeAsync(string tenantId, TenantThemeDto theme, CancellationToken ct = default);

    /// <summary>
    /// Сбрасывает тему указанного тенанта к значениям по умолчанию.
    /// </summary>
    Task ResetThemeAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Устанавливает тему указанного тенанта как тему по умолчанию для новых тенантов (только для корневого тенанта).
    /// </summary>
    Task SetAsDefaultThemeAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Сбрасывает закэшированную тему для указанного тенанта.
    /// </summary>
    Task InvalidateCacheAsync(string tenantId, CancellationToken ct = default);
}