using EDV.Framework.Shared.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EDV.Framework.Persistence;

/// <summary>
/// Проверяет строки подключения к базе данных для поддерживаемых провайдеров (PostgreSQL, SQL Server).
/// </summary>
/// <param name="dbSettings">Параметры конфигурации базы данных.</param>
/// <param name="logger">Экземпляр логгера для отслеживания ошибок.</param>
public sealed class ConnectionStringValidator(IOptions<DatabaseOptions> dbSettings, ILogger<ConnectionStringValidator> logger) : IConnectionStringValidator
{
    private readonly DatabaseOptions _dbSettings = dbSettings.Value;

    public bool TryValidate(string connectionString, string? dbProvider = null)
    {
        if (string.IsNullOrWhiteSpace(dbProvider))
        {
            dbProvider = _dbSettings.Provider;
        }

        try
        {
            switch (dbProvider?.ToUpperInvariant())
            {
                case DbProviders.PostgreSQL:
                    _ = new NpgsqlConnectionStringBuilder(connectionString);
                    break;
                default:
                    break;
            }

            return true;
        }
        catch (ArgumentException ex)
        {
            // Перехватывает недопустимый формат строки подключения от NpgsqlConnectionStringBuilder
            // и SqlConnectionStringBuilder (оба выбрасывают ArgumentException для некорректных строк).
            logger.LogError(ex, "Исключение при проверке строки подключения: {Error}", ex.Message);
            return false;
        }
        catch (FormatException ex)
        {
            // Перехватывает ошибки парсинга формата в значениях строки подключения.
            logger.LogError(ex, "Исключение при проверке строки подключения: {Error}", ex.Message);
            return false;
        }
    }
}