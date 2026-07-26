using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Multitenancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDV.Modules.Multitenancy.Data.Configurations;

public sealed class TenantExpiryNoticeConfiguration : IEntityTypeConfiguration<TenantExpiryNotice>
{
    public void Configure(EntityTypeBuilder<TenantExpiryNotice> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TenantExpiryNotices", MultitenancyConstants.Schema);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.NoticeType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ValidUptoUtc).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // Одно уведомление на тенанта для каждого состояния в рамках периода действия — гарантия дедупликации.
        builder.HasIndex(x => new { x.TenantId, x.NoticeType, x.ValidUptoUtc })
            .IsUnique()
            .HasDatabaseName("ux_tenant_expiry_notices_tenant_type_validupto");
    }
}
