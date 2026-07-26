using EDV.Modules.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDV.Modules.Billing.Data.Configurations;

public sealed class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WalletTransactions");
        builder.HasKey(x => x.Id);
        // Дочерняя сущность, достижимая только через навигацию Wallet.Transactions — фиксируем генерацию Id,
        // иначе EF помечает запись как Modified, а не как Added.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(64);
        builder.OwnsOne(x => x.Amount, m =>
        {
            m.Property(p => p.Amount).HasColumnName("Amount").HasPrecision(18, 4).IsRequired();
            m.Property(p => p.Currency).HasColumnName("Currency").HasMaxLength(8).IsRequired();
        });
        builder.Navigation(x => x.Amount).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.Description).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ReferenceId).HasMaxLength(128);
        builder.HasIndex(x => new { x.WalletId, x.CreatedAtUtc });
        builder.HasIndex(x => x.TenantId);

        // Гарантия однократного зачисления: не более одной строки журнала Topup на запрос пополнения
        // (ReferenceId == TopupRequest.Id). Конкурентный повторный вызов MarkInvoicePaid по тому же счёту
        // нарушает это ограничение и откатывается.
        builder.HasIndex(x => x.ReferenceId)
            .IsUnique()
            .HasFilter($"\"Kind\" = {(int)Contracts.WalletTransactionKind.Topup}")
            .HasDatabaseName("ux_wallet_transactions_topup_reference");
    }
}
