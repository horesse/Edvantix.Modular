namespace EDV.Framework.Eventing;

/// <summary>
/// Настройки строительного блока событийности.
/// </summary>
public sealed class EventingOptions
{
    /// <summary>
    /// Провайдер реализации шины событий. Поддерживаются: "InMemory", "RabbitMQ".
    /// </summary>
    public string Provider { get; set; } = "InMemory";

    /// <summary>
    /// Размер пакета для диспетчеризации outbox.
    /// </summary>
    public int OutboxBatchSize { get; set; } = 100;

    /// <summary>
    /// Максимальное число повторов, после которого сообщение outbox помечается как мёртвое.
    /// </summary>
    public int OutboxMaxRetries { get; set; } = 5;

    /// <summary>
    /// Базовая задержка (в секундах) для экспоненциального backoff после неудачной доставки.
    /// N-й повтор ждёт <c>base * 2^(n-1)</c>, ограничено сверху <see cref="OutboxRetryMaxDelaySeconds"/>.
    /// </summary>
    public int OutboxRetryBaseDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Верхняя граница (в секундах) экспоненциального backoff при повторах.
    /// </summary>
    public int OutboxRetryMaxDelaySeconds { get; set; } = 3600;

    /// <summary>
    /// Включена ли идемпотентная обработка на основе inbox.
    /// </summary>
    public bool EnableInbox { get; set; } = true;

    /// <summary>
    /// Интервал в секундах для фонового сервиса диспетчеризации outbox.
    /// Установите 0, чтобы отключить фоновый сервис (использовать вместо него Hangfire).
    /// </summary>
    public int OutboxDispatchIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Использовать ли хостед-сервис для диспетчеризации outbox.
    /// Если false, необходимо настроить Hangfire или другой планировщик.
    /// </summary>
    public bool UseHostedServiceDispatcher { get; set; } = true;
}
