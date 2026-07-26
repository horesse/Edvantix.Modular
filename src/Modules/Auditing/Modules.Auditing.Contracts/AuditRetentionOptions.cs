namespace EDV.Modules.Auditing.Contracts;

/// <summary>
/// Окна хранения, применяемые ежедневным заданием очистки аудита. Каждый тип события
/// может храниться независимо — события безопасности и исключений хранятся долго
/// для соответствия требованиям, активность хранится недолго, чтобы таблица оставалась
/// управляемой. По умолчанию "выключено" через флаг <see cref="Enabled"/>, чтобы
/// инсталляции включали это осознанно.
/// </summary>
public sealed class AuditRetentionOptions
{
    /// <summary>Главный переключатель задания очистки по хранению.</summary>
    public bool Enabled { get; set; }

    /// <summary>Срок хранения событий активности (HTTP/задание/команда).</summary>
    public int ActivityRetentionDays { get; set; } = 30;

    /// <summary>Срок хранения событий изменения сущностей.</summary>
    public int EntityChangeRetentionDays { get; set; } = 90;

    /// <summary>Срок хранения событий безопасности. Значение по умолчанию удобно для комплаенса.</summary>
    public int SecurityRetentionDays { get; set; } = 365;

    /// <summary>Срок хранения событий исключений.</summary>
    public int ExceptionRetentionDays { get; set; } = 180;

    /// <summary>
    /// Максимум строк, удаляемых за один вызов <c>ExecuteDeleteAsync</c>. Меньшие
    /// пакеты снижают нагрузку на блокировки Postgres ценой большего числа обращений.
    /// Задание работает в цикле, пока не удалит меньше строк, чем размер пакета.
    /// </summary>
    public int DeleteBatchSize { get; set; } = 5_000;

    /// <summary>
    /// Cron-выражение Hangfire для задания очистки. По умолчанию ежедневно в 03:30 UTC —
    /// нерабочее время для большинства часовых поясов, после большинства заданий отчётности.
    /// </summary>
    public string Cron { get; set; } = "30 3 * * *";
}
