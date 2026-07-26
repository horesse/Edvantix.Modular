using EDV.Framework.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDV.Framework.Eventing.Inbox;

/// <summary>
/// Сообщение inbox для отслеживания обработанных интеграционных событий по каждому обработчику
/// в целях идемпотентности потребителей.
///
/// Реализует <see cref="IGlobalEntity"/>, чтобы отказаться от автоматической фильтрации
/// по арендатору: потребители inbox работают в фоновых областях, а проверка "уже обработано"
/// должна выполняться across-tenant. Привязка к арендатору по строке хранится в явной
/// nullable-колонке <see cref="TenantId"/>.
/// </summary>
public class InboxMessage : IGlobalEntity
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = default!;

    public string HandlerName { get; set; } = default!;

    public DateTime ProcessedOnUtc { get; set; }

    public string? TenantId { get; set; }
}

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    private readonly string _schema;

    public InboxMessageConfiguration(string schema)
    {
        _schema = schema;
    }

    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("InboxMessages", _schema);

        builder.HasKey(i => new { i.Id, i.HandlerName });

        builder.Property(i => i.EventType)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(i => i.HandlerName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(i => i.TenantId)
            .HasMaxLength(64);
    }
}
