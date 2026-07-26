namespace EDV.Framework.Persistence;

/// <summary>
/// Интерфейс для операций инициализации базы данных, включая миграции и заполнение начальными данными.
/// </summary>
public interface IDbInitializer
{
    /// <summary>
    /// Применяет ожидающие миграции базы данных.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены для прерывания операции.</param>
    /// <returns>Задача, представляющая асинхронную операцию.</returns>
    Task MigrateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Заполняет базу данных начальными данными.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены для прерывания операции.</param>
    /// <returns>Задача, представляющая асинхронную операцию.</returns>
    Task SeedAsync(CancellationToken cancellationToken);
}