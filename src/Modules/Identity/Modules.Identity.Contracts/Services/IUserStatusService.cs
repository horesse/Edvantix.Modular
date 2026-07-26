namespace EDV.Modules.Identity.Contracts.Services;

/// <summary>
/// Сервис для операций с состоянием и жизненным циклом пользователей.
/// </summary>
public interface IUserStatusService
{
    /// <summary>
    /// Переключает статус активности пользователя.
    /// </summary>
    Task ToggleStatusAsync(bool activateUser, string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Мягко удаляет пользователя, деактивируя его.
    /// </summary>
    Task DeleteAsync(string userId, CancellationToken cancellationToken = default);
}