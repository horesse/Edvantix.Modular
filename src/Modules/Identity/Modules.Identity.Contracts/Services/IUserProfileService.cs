using EDV.Framework.Shared.Storage;
using EDV.Modules.Identity.Contracts.DTOs;

namespace EDV.Modules.Identity.Contracts.Services;

/// <summary>
/// Сервис для операций с профилем пользователя.
/// </summary>
public interface IUserProfileService
{
    /// <summary>
    /// Получает пользователя по ID.
    /// </summary>
    Task<UserDto> GetAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Получает всех пользователей.
    /// </summary>
    Task<List<UserDto>> GetListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Получает общее количество пользователей.
    /// </summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Обновляет профиль пользователя.
    /// </summary>
    Task UpdateAsync(string userId, string firstName, string lastName, string phoneNumber, FileUploadRequest image, bool deleteCurrentImage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Устанавливает URL изображения профиля напрямую (без загрузки). Используется в потоке с предварительно подписанным URL:
    /// клиент загружает через модуль Files, затем вызывает этот метод с полученным постоянным
    /// <c>publicUrl</c>. Передача <c>null</c> удаляет изображение.
    /// </summary>
    Task SetImageUrlAsync(string userId, string? imageUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Проверяет, существует ли пользователь с указанным email.
    /// </summary>
    Task<bool> ExistsWithEmailAsync(string email, string? exceptId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет, существует ли пользователь с указанным именем пользователя.
    /// </summary>
    Task<bool> ExistsWithNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет, существует ли пользователь с указанным номером телефона.
    /// </summary>
    Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, string? exceptId = null, CancellationToken cancellationToken = default);
}