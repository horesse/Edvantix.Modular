namespace EDV.Modules.Identity.Contracts.Services;

public interface IPasswordHistoryService
{
    /// <summary>Проверить, совпадает ли новый пароль с любыми недавними паролями в истории.</summary>
    Task<bool> IsPasswordInHistoryAsync(string userId, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Сохранить хеш текущего пароля в историю после смены пароля.</summary>
    Task SavePasswordHistoryAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Удалить старые записи истории паролей, превышающие настроенное количество сохраняемых.</summary>
    Task CleanupOldPasswordHistoryAsync(string userId, CancellationToken cancellationToken = default);
}