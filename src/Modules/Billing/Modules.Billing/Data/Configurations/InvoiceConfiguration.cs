using EDV.Modules.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDV.Modules.Billing.Data.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(64);
        builder.Property(x => x.InvoiceNumber).IsRequired().HasMaxLength(64);
        builder.Ignore(x => x.Currency);
        builder.OwnsOne(x => x.SubtotalAmount, m =>
        {
            m.Property(p => p.Amount).HasColumnName("SubtotalAmount").HasPrecision(18, 4).IsRequired();
            m.Property(p => p.Currency).HasColumnName("Currency").HasMaxLength(8).IsRequired();
        });
        builder.Navigation(x => x.SubtotalAmount).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Purpose).HasConversion<int>().HasDefaultValue(Contracts.InvoicePurpose.Usage);
        builder.Property(x => x.PeriodStartUtc);
        builder.Property(x => x.PeriodEndUtc);
        builder.Property(x => x.Notes).HasMaxLength(2048);

        // Один счёт на тенанта в месяц *для каждого назначения* — подписка (базовая плата за срок) и
        // использование (учитываемый перерасход) представляют собой отдельные потоки, которые могут
        // приходиться на один и тот же календарный месяц. Периодические счета уникальны в разрезе
        // тенант/период/назначение; счета на пополнение (Purpose=2) формируются по требованию и могут
        // повторяться в пределах периода, поэтому исключены из фильтра уникальности.
        builder.HasIndex(x => new { x.TenantId, x.PeriodYear, x.PeriodMonth, x.Purpose })
            .IsUnique()
            .HasFilter($"\"Purpose\" <> {(int)Contracts.InvoicePurpose.Topup}")
            .HasDatabaseName("ux_invoices_tenant_period_purpose");
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();

        builder.HasMany(x => x.LineItems)
            .WithOne()
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Invoice.LineItems))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.DomainEvents);
    }
}
