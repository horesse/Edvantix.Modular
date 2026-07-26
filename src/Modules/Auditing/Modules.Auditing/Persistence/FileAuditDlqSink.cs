using EDV.Modules.Auditing.Contracts;
using EDV.Modules.Auditing.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace EDV.Modules.Auditing.Persistence;

/// <summary>
/// Записывает конверты аудита, отправленные в мёртвые письма, как JSONL в файл с ежедневной
/// ротацией по пути <c>{ContentRoot}/audit-dlq/audit-dlq-{yyyy-MM-dd}.jsonl</c>.
///
/// Основан на файле намеренно: у него нет зависимости от Postgres, Redis или любой другой
/// инфраструктуры, которая, возможно, изначально стала причиной сбоя основного sink.
/// Ожидается, что операторы будут выгружать файл за пределы хоста (Filebeat, Vector и т.д.)
/// и воспроизводить его в хранилище отдельным процессом.
/// </summary>
public sealed class FileAuditDlqSink : IAuditDlqSink, IDisposable
{
    private readonly string _directory;
    private readonly IAuditSerializer _serializer;
    private readonly ILogger<FileAuditDlqSink> _log;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public FileAuditDlqSink(
        IHostEnvironment env,
        IAuditSerializer serializer,
        ILogger<FileAuditDlqSink> log,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(env);
        _serializer = serializer;
        _log = log;
        _timeProvider = timeProvider;
        _directory = Path.Combine(env.ContentRootPath, "audit-dlq");
    }

    public void Dispose() => _writeGate.Dispose();

    public async Task WriteAsync(IReadOnlyList<AuditEnvelope> batch, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0) return;

        try
        {
            Directory.CreateDirectory(_directory);
            var date = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var path = Path.Combine(_directory, $"audit-dlq-{date}.jsonl");

            // Строим строки вне блокировки, чтобы состязание покрывало только I/O.
            var sb = new StringBuilder(capacity: batch.Count * 256);
            foreach (var envelope in batch)
            {
                sb.Append(SerializeRecord(envelope)).Append('\n');
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            await _writeGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await using var fs = new FileStream(
                    path,
                    new FileStreamOptions
                    {
                        Mode = FileMode.Append,
                        Access = FileAccess.Write,
                        Share = FileShare.Read,
                        Options = FileOptions.Asynchronous,
                    });
                await fs.WriteAsync(bytes, ct).ConfigureAwait(false);
            }
            finally
            {
                _writeGate.Release();
            }

            AuditingTelemetry.DeadLettered.Add(batch.Count);
            _log.LogWarning("Отправлено в мёртвые письма {Count} событий аудита в {Path}.", batch.Count, path);
        }
        catch (Exception ex)
        {
            // DLQ — последний рубеж защиты. Если он тоже падает, просто логируем и не пробрасываем дальше —
            // воркеру некуда эскалировать.
            _log.LogError(ex, "Сбой записи DLQ аудита; потеряно {Count} событий.", batch.Count);
        }
    }

    private string SerializeRecord(AuditEnvelope envelope)
    {
        var record = new
        {
            envelope.Id,
            envelope.OccurredAtUtc,
            envelope.ReceivedAtUtc,
            EventType = envelope.EventType.ToString(),
            Severity = envelope.Severity.ToString(),
            envelope.TenantId,
            envelope.UserId,
            envelope.UserName,
            envelope.TraceId,
            envelope.SpanId,
            envelope.CorrelationId,
            envelope.RequestId,
            envelope.Source,
            Tags = envelope.Tags.ToString(),
            Payload = _serializer.SerializePayload(envelope.Payload),
        };
        return JsonSerializer.Serialize(record);
    }
}
