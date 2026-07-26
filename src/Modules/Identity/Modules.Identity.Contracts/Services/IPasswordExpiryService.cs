using EDV.Modules.Identity.Contracts.DTOs;

namespace EDV.Modules.Identity.Contracts.Services;

public interface IPasswordExpiryService
{
    /// <summary>Проверить, истёк ли срок действия пароля пользователя.</summary>
    Task<bool> IsPasswordExpiredAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Получить количество дней до истечения срока действия пароля (-1, если срок уже истёк).</summary>
    Task<int> GetDaysUntilExpiryAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Проверить, истекает ли срок действия пароля в ближайшее время (в пределах периода предупреждения).</summary>
    Task<bool> IsPasswordExpiringWithinWarningPeriodAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Получить статус истечения срока с подробной информацией.</summary>
    Task<PasswordExpiryStatusDto> GetPasswordExpiryStatusAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Обновить дату последней смены пароля для пользователя.</summary>
    Task UpdateLastPasswordChangeDateAsync(string userId, CancellationToken cancellationToken = default);
}