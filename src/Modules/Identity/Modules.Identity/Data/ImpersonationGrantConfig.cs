using EDV.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDV.Modules.Identity.Data;

public class ImpersonationGrantConfig : IEntityTypeConfiguration<ImpersonationGrant>
{
    public void Configure(EntityTypeBuilder<ImpersonationGrant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .ToTable("ImpersonationGrants", IdentityModuleConstants.SchemaName);

        // НЕ multitenant — межарендаторные имперсонации были бы отфильтрованы
        // Finbuckle. Фильтрация по арендатору выполняется на уровне запроса.

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Jti)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(g => g.Jti)
            .IsUnique();

        builder.Property(g => g.ActorUserId).IsRequired().HasMaxLength(64);
        builder.Property(g => g.ActorUserName).HasMaxLength(256);
        builder.Property(g => g.ActorTenantId).IsRequired().HasMaxLength(64);

        builder.Property(g => g.ImpersonatedUserId).IsRequired().HasMaxLength(64);
        builder.Property(g => g.ImpersonatedUserName).HasMaxLength(256);
        builder.Property(g => g.ImpersonatedTenantId).IsRequired().HasMaxLength(64);

        builder.Property(g => g.Reason).IsRequired().HasMaxLength(500);
        builder.Property(g => g.RevokeReason).HasMaxLength(500);
        builder.Property(g => g.RevokedByUserId).HasMaxLength(64);
        builder.Property(g => g.RevokedByUserName).HasMaxLength(256);

        builder.Property(g => g.ClientId).HasMaxLength(128);
        builder.Property(g => g.IpAddress).HasMaxLength(64);
        builder.Property(g => g.UserAgent).HasMaxLength(512);

        // Составной индекс поддерживает самый частый запрос: "активные гранты в
        // арендаторе X, сначала новые".
        builder.HasIndex(g => new { g.ImpersonatedTenantId, g.StartedAtUtc })
            .HasDatabaseName("IX_ImpersonationGrants_ImpersonatedTenantId_StartedAtUtc");

        builder.HasIndex(g => new { g.ActorUserId, g.StartedAtUtc })
            .HasDatabaseName("IX_ImpersonationGrants_ActorUserId_StartedAtUtc");
    }
}
