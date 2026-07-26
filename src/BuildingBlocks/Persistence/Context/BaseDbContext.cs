using EDV.Framework.Core.Domain;
using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Shared.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EDV.Framework.Persistence.Context;

/// <summary>
/// Базовый контекст базы данных с поддержкой мультиарендности и мягкого удаления.
/// </summary>
/// <param name="multiTenantContextAccessor">Аксессор для информации о контексте мультиарендности.</param>
/// <param name="options">Параметры контекста базы данных.</param>
/// <param name="settings">Настройки конфигурации базы данных.</param>
/// <param name="environment">Информация о хост-окружении.</param>
public class BaseDbContext(IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    DbContextOptions options,
    IOptions<DatabaseOptions> settings,
    IHostEnvironment environment)
    : MultiTenantDbContext(multiTenantContextAccessor, options)
{
    private readonly DatabaseOptions _settings = settings.Value;

    /// <summary>
    /// Настраивает модель, применяя глобальные фильтры запросов для функциональности мягкого удаления.
    /// </summary>
    /// <param name="modelBuilder">Построитель модели, используемый для настройки схемы базы данных.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, когда modelBuilder равен null.</exception>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.AppendGlobalQueryFilter<ISoftDeletable>(QueryFilters.SoftDelete, s => !s.IsDeleted);
        base.OnModelCreating(modelBuilder);
        // Изоляция арендаторов по умолчанию: сущности, не помеченные IGlobalEntity, получают IsMultiTenant().
        // Подклассы должны вызывать base.OnModelCreating ПОСЛЕ ApplyConfigurationsFromAssembly, чтобы конфигурации для каждой сущности были применены.
        modelBuilder.ApplyTenantIsolationByDefault();
    }

    /// <summary>
    /// Настраивает подключение к базе данных, используя строку подключения арендатора, если она доступна.
    /// </summary>
    /// <param name="optionsBuilder">Построитель параметров для настройки подключения к базе данных.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, когда optionsBuilder равен null.</exception>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        if (!string.IsNullOrWhiteSpace(multiTenantContextAccessor?.MultiTenantContext.TenantInfo?.ConnectionString))
        {
            optionsBuilder.ConfigureDatabase(
                _settings.Provider,
                multiTenantContextAccessor.MultiTenantContext.TenantInfo.ConnectionString!,
                _settings.MigrationsAssembly,
                environment.IsDevelopment());
        }
    }

    /// <summary>
    /// Сохраняет все изменения, внесённые в этот контекст, в базу данных с режимом перезаписи арендатора.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены для прерывания операции сохранения.</param>
    /// <returns>Количество записей состояния, записанных в базу данных.</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TenantNotSetMode = TenantNotSetMode.Overwrite;
        int result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}