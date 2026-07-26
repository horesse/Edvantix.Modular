using EDV.Modules.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Billing.Data;

/// <summary>
/// Данные биллинга хранятся в основной базе данных приложения, а не в базах данных отдельных
/// тенантов, поскольку счета и подписки — это административная область, требующая видимости
/// между тенантами. Принадлежность тенанту представлена явным столбцом <c>TenantId</c> и
/// фильтруется в сервисах запросов.
/// </summary>
public sealed class BillingDbContext : DbContext
{
    public const string Schema = "billing";

    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options) { }

    public DbSet<BillingPlan> Plans => Set<BillingPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<UsageSnapshot> UsageSnapshots => Set<UsageSnapshot>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<TopupRequest> TopupRequests => Set<TopupRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
    }
}
