using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System.Globalization;
using System.Net.Sockets;

namespace EDV.Starter.DbMigrator;

/// <summary>
/// Координирует конкурентные вызовы мигратора через сессионную консультативную блокировку Postgres
/// плюс цикл ожидания базы данных с экспоненциальной задержкой. Дешево, детерминированно,
/// не требует дополнительной инфраструктуры — и блокировка автоматически снимается при
/// закрытии удерживающего соединения (или при аварийном завершении процесса мигратора).
/// </summary>
internal static partial class PostgresMigratorLock
{
    // Произвольный 64-битный ключ (видимый в pg_locks) для блокировки сессии edv-db-migrator.
    // Удерживается на уровне сервера, поэтому целевая база данных не влияет на координацию между экземплярами.
    private const long MigratorAdvisoryLockKey = unchecked((long)0xFE514EC0_DEB1ADE4UL);

    // Параметризованный SQL: ключ bigint является константой, но параметр удовлетворяет
    // CA2100 (проверка SQL-инъекций) без отключения анализатора.
    private const string AcquireSql = "SELECT pg_advisory_lock(@key)";
    private const string ReleaseSql = "SELECT pg_advisory_unlock(@key)";

    /// <summary>
    /// Опрашивает настроенную базу данных, пока она не примет соединение — обрабатывает
    /// холодный старт Aspire/K8s, когда Postgres требуется несколько секунд, чтобы стать
    /// доступным. Экспоненциальная задержка до 10 секунд на попытку, ограниченная общим
    /// дедлайном. Возвращает, когда:
    ///   · база данных принимает соединение (сервер готов), ИЛИ
    ///   · соединение падает с SQLSTATE 3D000 "база данных не существует"
    ///     — сервер доступен; EF создаст базу данных при Migrate.
    /// </summary>
    public static async Task WaitForDatabaseAsync(
        string connectionString,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var delay = TimeSpan.FromSeconds(1);
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        var attempt = 0;

        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
                LogPostgresReady(logger, attempt);
                return;
            }
            catch (PostgresException ex) when (ex.SqlState == "3D000")
            {
                // Сервер доступен; целевая база данных ещё не существует. EF
                // создаст её при первом вызове MigrateAsync.
                LogTargetDatabaseMissing(logger, ex);
                return;
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException or SocketException)
            {
                LogPostgresNotReady(logger, ex, attempt, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 10));
            }
        }

        throw new TimeoutException(string.Create(
            CultureInfo.InvariantCulture,
            $"Postgres не стал доступен в течение 2 минут (после {attempt} попыток)."));
    }

    /// <summary>
    /// Захватывает консультативную блокировку мигратора — блокируется, пока она не станет доступна,
    /// поэтому конкурентные вызовы мигратора автоматически сериализуются. Возвращаемый
    /// IDisposable удерживает выделенное соединение; его освобождение (или аварийное
    /// завершение процесса) снимает блокировку.
    ///
    /// При первом запуске, когда целевая база данных не существует, возвращается
    /// заглушка без блокировки — защищать пока нечего, EF создаст базу данных
    /// при MigrateAsync, а последующие запуски получат реальную блокировку.
    /// </summary>
    public static async Task<IAsyncDisposable> AcquireAsync(
        string connectionString,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var conn = new NpgsqlConnection(connectionString);
        try
        {
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == "3D000")
        {
            LogSkipLockForMissingDb(logger, ex);
            await conn.DisposeAsync().ConfigureAwait(false);
            return NoopLock.Instance;
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = AcquireSql;
            cmd.Parameters.Add(new NpgsqlParameter("key", NpgsqlDbType.Bigint) { Value = MigratorAdvisoryLockKey });
            LogAcquiringLock(logger, MigratorAdvisoryLockKey);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        LogLockAcquired(logger);

        return new LockHolder(conn, logger);
    }

    private sealed class LockHolder : IAsyncDisposable
    {
        private readonly NpgsqlConnection _conn;
        private readonly ILogger _logger;

        public LockHolder(NpgsqlConnection conn, ILogger logger)
        {
            _conn = conn;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            // Закрытие соединения автоматически снимает все консультативные блокировки
            // уровня сессии, удерживаемые на нём. Явное разблокирование удобно для логирования.
            try
            {
                await using var cmd = _conn.CreateCommand();
                cmd.CommandText = ReleaseSql;
                cmd.Parameters.Add(new NpgsqlParameter("key", NpgsqlDbType.Bigint) { Value = MigratorAdvisoryLockKey });
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                LogLockReleased(_logger);
            }
            catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
            {
                // С максимальными усилиями — закрытие соединения ниже всё равно снимает блокировку.
                LogUnlockBestEffortFail(_logger, ex);
            }
            finally
            {
                await _conn.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private sealed class NoopLock : IAsyncDisposable
    {
        public static readonly NoopLock Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // LoggerMessage source-gen: шаблоны времени компиляции избегают CA1873 (немедленное вычисление аргументов)
    // и хорошо сочетаются с S6667 (логирование в блоке catch передаёт исключение явно).

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Postgres готов (попытка {Attempt}).")]
    private static partial void LogPostgresReady(ILogger logger, int attempt);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Postgres доступен, но целевая база данных ещё не существует — EF создаст её.")]
    private static partial void LogTargetDatabaseMissing(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Postgres не готов (попытка {Attempt}). Повтор через {Delay}с…")]
    private static partial void LogPostgresNotReady(ILogger logger, Exception ex, int attempt, double delay);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "Целевая база данных не существует — пропускаем консультативную блокировку для первого запуска; EF создаст её.")]
    private static partial void LogSkipLockForMissingDb(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "Захват консультативной блокировки DbMigrator (ключ {Key:X16})…")]
    private static partial void LogAcquiringLock(ILogger logger, long key);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information,
        Message = "Консультативная блокировка DbMigrator захвачена.")]
    private static partial void LogLockAcquired(ILogger logger);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information,
        Message = "Консультативная блокировка DbMigrator освобождена.")]
    private static partial void LogLockReleased(ILogger logger);

    [LoggerMessage(EventId = 8, Level = LogLevel.Debug,
        Message = "Явное разблокирование не удалось; закрытие соединения освободит блокировку.")]
    private static partial void LogUnlockBestEffortFail(ILogger logger, Exception ex);
}