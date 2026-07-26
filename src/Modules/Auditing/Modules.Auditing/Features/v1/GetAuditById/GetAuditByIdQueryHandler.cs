using EDV.Modules.Auditing.Contracts;
using EDV.Modules.Auditing.Contracts.Dtos;
using EDV.Modules.Auditing.Contracts.v1.GetAuditById;
using EDV.Modules.Auditing.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EDV.Modules.Auditing.Features.v1.GetAuditById;

public sealed class GetAuditByIdQueryHandler : IQueryHandler<GetAuditByIdQuery, AuditDetailDto>
{
    private readonly AuditDbContext _dbContext;
    private readonly ILogger<GetAuditByIdQueryHandler> _logger;

    public GetAuditByIdQueryHandler(AuditDbContext dbContext, ILogger<GetAuditByIdQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<AuditDetailDto> Handle(GetAuditByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var record = await _dbContext.AuditRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == query.Id, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            // KeyNotFoundException глобально мапится на 404. Оставлено (а не фреймворковый NotFoundException),
            // потому что фикстуры типов исключений аудита и классификация серьёзности завязаны именно на этот тип.
            throw new KeyNotFoundException($"Запись аудита {query.Id} не найдена.");
        }

        JsonElement payload;
        try
        {
            using var document = JsonDocument.Parse(record.PayloadJson);
            payload = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Не удалось разобрать JSON payload аудита для записи {AuditId}.", query.Id);
            payload = JsonDocument.Parse("{}").RootElement.Clone();
        }

        return new AuditDetailDto
        {
            Id = record.Id,
            OccurredAtUtc = record.OccurredAtUtc,
            ReceivedAtUtc = record.ReceivedAtUtc,
            EventType = (AuditEventType)record.EventType,
            Severity = (AuditSeverity)record.Severity,
            TenantId = record.TenantId,
            UserId = record.UserId,
            UserName = record.UserName,
            TraceId = record.TraceId,
            SpanId = record.SpanId,
            CorrelationId = record.CorrelationId,
            RequestId = record.RequestId,
            Source = record.Source,
            Tags = (AuditTag)record.Tags,
            Payload = payload
        };
    }
}