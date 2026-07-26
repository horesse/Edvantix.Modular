namespace EDV.Framework.Persistence;

/// <summary>
/// Интерфейс для проверки строк подключения к базе данных.
/// </summary>
public interface IConnectionStringValidator
{
    /// <summary>
    /// Проверяет формат и доступность указанной строки подключения.
    /// </summary>
    /// <param name="connectionString">Строка подключения для проверки.</param>
    /// <param name="dbProvider">Необязательный тип провайдера базы данных для специфичной проверки.</param>
    /// <returns>true, если строка подключения допустима; иначе false.</returns>
    bool TryValidate(string connectionString, string? dbProvider = null);
}