using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDV.Modules.Auditing.Persistence;

public class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("AuditRecords", "audit");
        builder.IsMultiTenant();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasConversion<int>();
        builder.Property(x => x.Severity).HasConversion<byte>();
        builder.Property(x => x.Tags).HasConversion<long>();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb");

        // Индекс горячего пути: список аудита по умолчанию фильтрует по TenantId (Finbuckle) и сортирует по OccurredAtUtc DESC.
        // Составной индекс по обоим полям позволяет PostgreSQL обслуживать постраничный top-N через index-only walk.
        builder.HasIndex(x => new { x.TenantId, x.OccurredAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_AuditRecords_Tenant_OccurredAt");

        // Частый срез дашборда: EventType в рамках арендатора, упорядоченный по времени. Обходит
        // индекс (TenantId, OccurredAtUtc), когда EventType селективен (например, только события Security).
        builder.HasIndex(x => new { x.TenantId, x.EventType, x.OccurredAtUtc })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_AuditRecords_Tenant_EventType_OccurredAt");

        // Поиск по trace/correlation ("всё, связанное с этим запросом"). Обе колонки разрежены,
        // поэтому одноколоночные индексы дёшевы и часто селективны.
        builder.HasIndex(x => x.CorrelationId)
            .HasDatabaseName("IX_AuditRecords_CorrelationId");
        builder.HasIndex(x => x.TraceId)
            .HasDatabaseName("IX_AuditRecords_TraceId");

        // ILIKE-поиск по Source / UserName: GIN-индексы pg_trgm превращают `%term%` из seq scan в probe.
        // (Расширение pg_trgm создаётся на уровне контекста.)
        builder.HasIndex(x => x.Source)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("IX_AuditRecords_Source_trgm");
        builder.HasIndex(x => x.UserName)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("IX_AuditRecords_UserName_trgm");

        // GIN по jsonb через jsonb_path_ops: поддерживает containment (@>, ?) при гораздо меньшем расходе диска, чем jsonb_ops по умолчанию.
        // ILIKE по сырому тексту JSON всё равно делает seq scan — для этого используйте индексированные колонки (Source, UserName) или денормализацию.
        builder.HasIndex(x => x.PayloadJson)
            .HasMethod("gin")
            .HasOperators("jsonb_path_ops")
            .HasDatabaseName("IX_AuditRecords_PayloadJson_gin");
    }
}
