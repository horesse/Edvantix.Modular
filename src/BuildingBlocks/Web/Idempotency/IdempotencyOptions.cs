namespace EDV.Framework.Web.Idempotency;

/// <summary>
/// Параметры конфигурации для идемпотентности HTTP-запросов.
/// </summary>
public sealed class IdempotencyOptions
{
    /// <summary>
    /// Имя заголовка, из которого считывается ключ идемпотентности. По умолчанию: "Idempotency-Key".
    /// </summary>
    public string HeaderName { get; set; } = "Idempotency-Key";

    /// <summary>
    /// Время жизни по умолчанию для кэшированных идемпотентных ответов. По умолчанию: 24 часа.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Максимально допустимая длина ключа идемпотентности. По умолчанию: 128 символов.
    /// </summary>
    public int MaxKeyLength { get; set; } = 128;
}