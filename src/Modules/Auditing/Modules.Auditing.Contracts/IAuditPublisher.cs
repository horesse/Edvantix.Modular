namespace EDV.Modules.Auditing.Contracts;

/// <summary>
/// Публикатор с низкой задержкой, неблокирующий. Реализуйте через ограниченный канал + фоновый воркер.
/// </summary>
public interface IAuditPublisher
{
    /// <summary>Публикует событие аудита. Реализации не должны блокировать путь обработки запроса.</summary>
    ValueTask PublishAsync(IAuditEvent auditEvent, CancellationToken ct = default);

    /// <summary>Окружающая область для текущей операции (обычно в рамках запроса).</summary>
    IAuditScope CurrentScope { get; }
}
