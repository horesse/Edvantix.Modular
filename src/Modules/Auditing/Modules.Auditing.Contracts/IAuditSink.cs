namespace EDV.Modules.Auditing.Contracts;

/// <summary>
/// Приёмник для событий аудита (например, SQL, файл, OTLP). Реализации должны быть эффективными и работать с пакетами.
/// </summary>
public interface IAuditSink
{
    Task WriteAsync(IReadOnlyList<AuditEnvelope> batch, CancellationToken ct);
}