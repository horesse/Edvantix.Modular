using EDV.Framework.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDV.Framework.Eventing.Outbox;

/// <summary>
/// Сущность сообщения outbox, используемая для сохранения интеграционных событий вместе с
/// изменениями домена.
///
/// Реализует <see cref="IGlobalEntity"/>, чтобы отказаться от автоматической фильтрации
/// по арендатору: обработчики outbox работают в фоновых областях без контекста арендатора
/// и должны сканировать строки across-tenant. Привязка к арендатору по строке хранится
/// в явной nullable-колонке <see cref="TenantId"/>.
/// </summary>
public class OutboxMessage : IGlobalEntity
{
    public Guid Id { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public string Type { get; set; } = default!;

    public string Payload { get; set; } = default!;

    public string? TenantId { get; set; }

    public string? CorrelationId { get; set; }

    public DateTime? ProcessedOnUtc { get; set; }

    public int RetryCount { get; set; }

    public string? LastError { get; set; }

    public bool IsDead { get; set; }

    /// <summary>
    /// Ближайшее время в UTC, когда сообщение снова станет доступно для попытки диспетчеризации.
    /// Устанавливается в будущее время с учётом backoff после сбоя, чтобы повторы не срабатывали
    /// на каждом цикле диспетчеризации; <c>null</c> означает немедленную доступность
    /// (новое сообщение или после восстановления из мёртвых).
    /// </summary>
    public DateTime? NextRetryAt { get; set; }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    private readonly string _schema;

    public OutboxMessageConfiguration(string schema)
    {
        _schema = schema;
    }

    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OutboxMessages", _schema);

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Type)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(o => o.Payload)
            .IsRequired();

        builder.Property(o => o.TenantId)
            .HasMaxLength(64);

        builder.Property(o => o.CorrelationId)
            .HasMaxLength(128);

        builder.Property(o => o.CreatedOnUtc)
            .IsRequired();
    }
}
