using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace EDV.Framework.Persistence;

/// <summary>
/// Внутренние методы расширения для конфигурации ModelBuilder в Entity Framework.
/// </summary>
internal static class ModelBuilderExtensions
{
    /// <summary>
    /// Регистрирует именованный глобальный фильтр запросов для каждой сущности, реализующей
    /// <typeparamref name="TInterface"/>. Именованные фильтры комбинируются с анонимными
    /// фильтрами (например, фильтром арендаторов Finbuckle) и другими именованными фильтрами
    /// через AND во время выполнения запроса. Чтобы отключить только этот фильтр в конкретном месте
    /// вызова, используйте <c>queryable.IgnoreQueryFilters([filterName])</c> — анонимные и
    /// другие именованные фильтры останутся активными.
    /// </summary>
    /// <typeparam name="TInterface">Тип интерфейса, по которому фильтруются сущности.</typeparam>
    /// <param name="modelBuilder">Экземпляр ModelBuilder для настройки.</param>
    /// <param name="filterName">Стабильное имя фильтра (см. <see cref="QueryFilters"/>).</param>
    /// <param name="filter">Выражение фильтра для применения ко всем соответствующим сущностям.</param>
    /// <returns>ModelBuilder для цепочки вызовов.</returns>
    public static ModelBuilder AppendGlobalQueryFilter<TInterface>(
        this ModelBuilder modelBuilder,
        string filterName,
        Expression<Func<TInterface, bool>> filter)
    {
        var entities = modelBuilder.Model.GetEntityTypes()
            .Where(e => e.BaseType is null && e.ClrType.GetInterface(typeof(TInterface).Name) is not null)
            .Select(e => e.ClrType);

        foreach (var entity in entities)
        {
            var parameterType = Expression.Parameter(modelBuilder.Entity(entity).Metadata.ClrType);
            var filterBody = ReplacingExpressionVisitor.Replace(filter.Parameters.Single(), parameterType, filter.Body);
            modelBuilder.Entity(entity).HasQueryFilter(filterName, Expression.Lambda(filterBody, parameterType));
        }

        return modelBuilder;
    }
}