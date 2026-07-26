namespace EDV.Framework.Web.HttpResilience;

/// <summary>
/// Параметры конфигурации для конвейеров устойчивости HTTP-клиента (повторы, автоматический выключатель, таймаут).
/// </summary>
public sealed class HttpResilienceOptions
{
    /// <summary>
    /// Включены ли обработчики устойчивости. По умолчанию: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Максимальное количество попыток повтора. По умолчанию: 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Медианная задержка для первого повтора (экспоненциальная задержка). По умолчанию: 1 секунда.
    /// </summary>
    public TimeSpan MedianFirstRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Общий таймаут для всего запроса, включая все повторы. По умолчанию: 30 секунд.
    /// </summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Таймаут для каждой отдельной попытки. По умолчанию: 10 секунд.
    /// </summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Длительность, в течение которой автоматический выключатель остаётся открытым после срабатывания. По умолчанию: 5 секунд.
    /// </summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Коэффициент отказов, при котором срабатывает автоматический выключатель. По умолчанию: 0.5 (50%).
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Минимальная пропускная способность перед оценкой автоматического выключателя. По умолчанию: 10 запросов.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;
}