namespace EDV.Framework.Eventing.RabbitMq;

/// <summary>
/// Настройки шины событий RabbitMQ.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>
    /// Хост RabbitMQ или строка подключения.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Порт RabbitMQ. По умолчанию 5672.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Имя пользователя для аутентификации в RabbitMQ.
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// Пароль для аутентификации в RabbitMQ.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Виртуальный хост. По умолчанию "/".
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Имя обменника для публикации событий. По умолчанию "edv.events".
    /// </summary>
    public string ExchangeName { get; set; } = "edv.events";

    /// <summary>
    /// Префикс имени очереди для потребления событий. По умолчанию "edv".
    /// </summary>
    public string QueuePrefix { get; set; } = "edv";

    /// <summary>
    /// Включить SSL/TLS-подключение. По умолчанию false.
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Количество попыток повтора публикации. По умолчанию 3.
    /// </summary>
    public int PublishRetryCount { get; set; } = 3;

    /// <summary>
    /// Задержка между повторами в миллисекундах. По умолчанию 1000.
    /// </summary>
    public int PublishRetryDelayMs { get; set; } = 1000;
}
