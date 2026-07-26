using EDV.Framework.Eventing.Inbox;
using EDV.Framework.Eventing.Outbox;
using EDV.Framework.Persistence;
using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Shared.Persistence;
using EDV.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EDV.Modules.Identity.Data;

public class IdentityDbContext : MultiTenantIdentityDbContext<AppUser,
    AppRole,
    string,
    IdentityUserClaim<string>,
    IdentityUserRole<string>,
    IdentityUserLogin<string>,
    AppRoleClaim,
    IdentityUserToken<string>,
    IdentityUserPasskey<string>>
{
    private readonly DatabaseOptions _settings;
    private new AppTenantInfo TenantInfo { get; set; }
    private readonly IHostEnvironment _environment;
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<GroupRole> GroupRoles => Set<GroupRole>();

    public DbSet<UserGroup> UserGroups => Set<UserGroup>();

    public DbSet<ImpersonationGrant> ImpersonationGrants => Set<ImpersonationGrant>();

    public IdentityDbContext(
        IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
        DbContextOptions<IdentityDbContext> options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment) : base(multiTenantContextAccessor, options)
    {
        ArgumentNullException.ThrowIfNull(multiTenantContextAccessor);
        ArgumentNullException.ThrowIfNull(settings);

        _environment = environment;
        _settings = settings.Value;
        TenantInfo = multiTenantContextAccessor.MultiTenantContext.TenantInfo!;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        builder.ApplyConfiguration(new OutboxMessageConfiguration(IdentityModuleConstants.SchemaName));
        builder.ApplyConfiguration(new InboxMessageConfiguration(IdentityModuleConstants.SchemaName));

        // Изоляция по арендатору включена по умолчанию: сущности, не реализующие IGlobalEntity, автоматически получают IsMultiTenant() (Outbox/Inbox/ImpersonationGrant исключены).
        // Таблицы Identity уже помечены IsMultiTenant в IdentityConfigurations.cs; автоприменение обнаруживает эту аннотацию и пропускает их.
        builder.ApplyTenantIsolationByDefault();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!string.IsNullOrWhiteSpace(TenantInfo?.ConnectionString))
        {
            optionsBuilder.ConfigureDatabase(
                _settings.Provider,
                TenantInfo.ConnectionString,
                _settings.MigrationsAssembly,
                _environment.IsDevelopment());
        }
    }
}