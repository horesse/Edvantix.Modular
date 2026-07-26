using EDV.Framework.Core.Domain;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace EDV.Framework.Persistence;

/// <summary>
/// Настройки изоляции арендаторов по умолчанию для <see cref="ModelBuilder"/>. Применяются из
/// <see cref="Context.BaseDbContext"/> и <c>IdentityDbContext</c>, поэтому каждая
/// сущность в модели ограничена арендатором, если она явно не отказывается от этого с помощью
/// маркерного интерфейса <see cref="IGlobalEntity"/>. Это делает поведение ПО УМОЛЧАНИЮ
/// изолированным по арендаторам — добавление новой сущности в модуль больше не может
/// незаметно привести к утечке данных между арендаторами.
/// </summary>
public static class TenantIsolationExtensions
{
    /// <summary>Ключ аннотации Finbuckle для каждой сущности. Чтение этого значения позволяет пропустить
    /// сущности, которые уже явно включены через <c>builder.IsMultiTenant()</c>.</summary>
    private const string FinbuckleMultiTenantAnnotation = "Finbuckle:MultiTenant";

    /// <summary>
    /// Проходит по всем невладеемым сущностям в <paramref name="modelBuilder"/> и
    /// помечает их как <c>IsMultiTenant().AdjustUniqueIndexes()</c>, если они не являются
    /// <see cref="IGlobalEntity"/> или уже явно не отмечены.
    ///
    /// Вызывайте ПОСЛЕ <c>ApplyConfigurationsFromAssembly</c>, чтобы конфигурации для каждой
    /// сущности (уникальные индексы, владеемые типы) уже были применены — методу AdjustUniqueIndexes
    /// от Finbuckle они нужны, чтобы знать, что расширять.
    /// </summary>
    public static void ApplyTenantIsolationByDefault(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned()) continue;
            if (entityType.ClrType is null) continue;
            // Пропускаем модели-подтипы без ключа (например, IdentityPasskeyData), которые присутствуют
            // в модели EF, но не являются сохраняемыми сущностями; вызов IsMultiTenant() для них
            // выбрасывает исключение "missing primary key".
            if (entityType.FindPrimaryKey() is null) continue;
            if (typeof(IGlobalEntity).IsAssignableFrom(entityType.ClrType)) continue;
            if (entityType.FindAnnotation(FinbuckleMultiTenantAnnotation) is not null) continue;

            modelBuilder.Entity(entityType.ClrType).IsMultiTenant().AdjustUniqueIndexes();
        }
    }
}