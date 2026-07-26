using Finbuckle.MultiTenant.Abstractions;
using EDV.Framework.Core.Domain;
using EDV.Framework.Persistence.Context;
using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using System.Reflection;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Обеспечивает соблюдение контракта «изоляция по арендатору по умолчанию», введённого 2026-05-17.
/// Каждая конкретная сущность, достижимая из DbContext, унаследованного от <see cref="BaseDbContext"/>,
/// должна в итоге получить аннотацию Finbuckle <c>IsMultiTenant()</c> — либо явно, в
/// <c>IEntityTypeConfiguration</c>, либо через автоприменение в <c>BaseDbContext.OnModelCreating</c>.
/// Отказаться от этого можно через маркерный интерфейс <see cref="IGlobalEntity"/> (используется для
/// общеплатформенных строк, таких как BillingPlan, ImpersonationGrant, OutboxMessage, InboxMessage).
///
/// Этот тест отлавливает класс ошибок с «тихой утечкой», когда кто-то добавляет новую
/// сущность в модуль, забывает пометить её как multitenant и выпускает изменение — в итоге
/// «ничейные» строки становятся видны каждому арендатору.
/// </summary>
public sealed class TenantIsolationTests
{
    private const string FinbuckleAnnotation = "Finbuckle:MultiTenant";

    /// <summary>
    /// Каждый DbContext, унаследованный от <see cref="BaseDbContext"/>, в загруженных сборках модулей
    /// создаётся; мы проверяем, что каждая невладеемая (non-owned) сущность в его модели либо имеет
    /// аннотацию Finbuckle, либо реализует <see cref="IGlobalEntity"/>.
    /// </summary>
    [Fact]
    public void BaseDbContext_Entities_Should_Be_TenantIsolated_Or_Marked_Global()
    {
        var violations = new List<string>();

        foreach (var ctxType in DiscoverBaseDbContextTypes())
        {
            using var ctx = ConstructDbContext(ctxType);
            var model = ctx.Model;

            foreach (var entityType in model.GetEntityTypes())
            {
                if (entityType.IsOwned()) continue;
                if (entityType.ClrType is null) continue;
                if (entityType.FindPrimaryKey() is null) continue;
                if (typeof(IGlobalEntity).IsAssignableFrom(entityType.ClrType)) continue;

                if (entityType.FindAnnotation(FinbuckleAnnotation) is null)
                {
                    violations.Add($"{ctxType.Name} → {entityType.ClrType.FullName} не имеет IsMultiTenant() и не помечена как IGlobalEntity");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Каждая сущность в DbContext, унаследованном от BaseDbContext, должна быть изолирована по арендатору. " +
            "Примените builder.IsMultiTenant() в конфигурации EF, ИЛИ откажитесь от изоляции, реализовав " +
            "EDV.Framework.Core.Domain.IGlobalEntity (только для сущностей, которые действительно " +
            "являются общеплатформенными, например BillingPlan или ImpersonationGrant). " +
            $"Нарушения:\n  {string.Join("\n  ", violations)}");
    }

    private static IEnumerable<Type> DiscoverBaseDbContextTypes()
    {
        return ModuleAssemblyDiscovery.GetModuleAssemblies()
            .SelectMany(a => SafeGetTypes(a))
            .Where(t => t.IsClass && !t.IsAbstract && typeof(BaseDbContext).IsAssignableFrom(t));
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly a)
    {
        try { return a.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }

    /// <summary>
    /// BaseDbContext принимает (accessor, options, settings, environment). Мы подменяем
    /// каждый параметр минимальной заглушкой, достаточной для достижения OnModelCreating.
    /// Конкретный тип DbContext формирует DbContextOptions&lt;T&gt; через рефлексию,
    /// так что не приходится вручную писать по одному на каждый модуль.
    /// </summary>
    private static DbContext ConstructDbContext(Type dbContextType)
    {
        var optionsType = typeof(DbContextOptions<>).MakeGenericType(dbContextType);
        var builderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType);
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(builderType)!;
        // Провайдер Npgsql делает подключение для каждого арендатора в OnConfiguring no-op (пустая ConnectionString);
        // модель строится лениво при первом обращении к ctx.Model, поэтому подключение к БД не открывается.
        builder.UseNpgsql("Host=arch;Database=arch;Username=arch;Password=arch");
        var options = builder.Options;

        var settings = Options.Create(new DatabaseOptions
        {
            Provider = "postgresql",
            ConnectionString = string.Empty,
            MigrationsAssembly = "EDV.Starter.Migrations.PostgreSQL",
        });

        var ctor = dbContextType.GetConstructor([
            typeof(IMultiTenantContextAccessor<AppTenantInfo>),
            optionsType,
            typeof(IOptions<DatabaseOptions>),
            typeof(IHostEnvironment),
        ]) ?? throw new InvalidOperationException(
            $"{dbContextType.Name} не имеет ожидаемой сигнатуры конструктора BaseDbContext.");

        return (DbContext)ctor.Invoke([
            new StubAccessor(),
            options,
            settings,
            new StubEnvironment(),
        ]);
    }

    private sealed class StubAccessor : IMultiTenantContextAccessor<AppTenantInfo>
    {
        public IMultiTenantContext<AppTenantInfo> MultiTenantContext { get; set; } =
            new MultiTenantContext<AppTenantInfo>(
                new AppTenantInfo("arch", "arch", string.Empty, "arch@arch", "arch"));

        IMultiTenantContext IMultiTenantContextAccessor.MultiTenantContext => MultiTenantContext;
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "arch";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
