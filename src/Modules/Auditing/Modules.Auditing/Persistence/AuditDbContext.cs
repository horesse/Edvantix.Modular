using EDV.Framework.Persistence.Context;
using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Shared.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;
using System.Reflection;

namespace EDV.Modules.Auditing.Persistence;

public sealed class AuditDbContext : BaseDbContext
{
    public AuditDbContext(
    IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    DbContextOptions<AuditDbContext> options,
    IOptions<DatabaseOptions> settings,
    IHostEnvironment environment) : base(multiTenantContextAccessor, options, settings, environment) { }

    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Требуется для триграммных GIN-индексов по Source/UserName. Операция идемпотентна —
        // повторные применения безопасны; роли, выполняющей миграцию, нужны права CREATE на базу данных.
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);

        // Отображаем AuditJsonbFunctions.AsText на `CAST(x AS text)`, чтобы jsonb PayloadJson был доступен для ILIKE-поиска.
        // Без приведения типа ILIKE по jsonb выбрасывает ("like_escape(jsonb, unknown) does not exist") → HTTP 500.
        var textMapping = this.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(string))!;
        var asTextMethod = typeof(AuditJsonbFunctions)
            .GetMethod(nameof(AuditJsonbFunctions.AsText), BindingFlags.Public | BindingFlags.Static)!;
        modelBuilder
            .HasDbFunction(asTextMethod)
            .HasTranslation(args => new SqlUnaryExpression(
                ExpressionType.Convert,
                args[0],
                typeof(string),
                textMapping));

        // base.OnModelCreating выполняется ПОСЛЕДНИМ, чтобы автоприменение BaseDbContext видело
        // полностью настроенные сущности (включая дочерние типы HasMany).
        base.OnModelCreating(modelBuilder);
    }
}