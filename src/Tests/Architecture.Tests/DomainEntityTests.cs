using EDV.Framework.Core.Domain;
using Shouldly;
using System.Reflection;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Тесты для соблюдения паттернов DDD в доменных сущностях.
/// </summary>
public class DomainEntityTests
{
    private static readonly Assembly[] ModuleAssemblies = ModuleAssemblyDiscovery.GetModuleAssemblies();

    [Fact]
    public void Domain_Events_Should_Implement_IDomainEvent()
    {
        var failures = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            var eventTypes = module.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.Name.EndsWith("DomainEvent", StringComparison.Ordinal)
                         || t.Name.EndsWith("Event", StringComparison.Ordinal)
                             && t.Namespace?.Contains(".Domain", StringComparison.Ordinal) == true);

            foreach (var eventType in eventTypes)
            {
                if (!typeof(IDomainEvent).IsAssignableFrom(eventType))
                {
                    failures.Add($"{eventType.FullName} должен реализовывать IDomainEvent");
                }
            }
        }

        failures.ShouldBeEmpty(
            $"Все доменные события должны реализовывать IDomainEvent. " +
            $"Нарушения: {string.Join(", ", failures)}");
    }

    [Fact]
    public void Domain_Events_Should_Be_Sealed()
    {
        var failures = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            var eventTypes = module.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => typeof(IDomainEvent).IsAssignableFrom(t));

            foreach (var eventType in eventTypes)
            {
                // Проверяем, является ли тип record (records тоже могут быть sealed)
                bool isRecord = eventType.GetProperty("EqualityContract",
                    BindingFlags.NonPublic | BindingFlags.Instance) != null;

                if (!eventType.IsSealed && !isRecord)
                {
                    failures.Add($"{eventType.FullName} должен быть sealed или record");
                }
            }
        }

        failures.ShouldBeEmpty(
            $"Доменные события должны быть sealed или record для обеспечения неизменяемости. " +
            $"Нарушения: {string.Join(", ", failures)}");
    }

    [Fact]
    public void Entities_In_Core_Namespace_Should_Implement_IEntity()
    {
        var failures = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            var entityTypes = module.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.Namespace?.Contains(".Core.", StringComparison.Ordinal) == true)
                .Where(t => t.Name.EndsWith("Entity", StringComparison.Ordinal)
                         || (t.Namespace?.Contains(".Domain", StringComparison.Ordinal) == true
                             && !t.Name.EndsWith("Event", StringComparison.Ordinal)
                             && !t.Name.EndsWith("Dto", StringComparison.Ordinal)
                             && !t.Name.EndsWith("Exception", StringComparison.Ordinal)));

            foreach (var entityType in entityTypes)
            {
                bool implementsIEntity = entityType.GetInterfaces()
                    .Any(i => i.IsGenericType &&
                              i.GetGenericTypeDefinition().Name.StartsWith("IEntity", StringComparison.Ordinal));

                bool inheritsBaseEntity = IsSubclassOfGeneric(entityType, typeof(BaseEntity<>));

                if (!implementsIEntity && !inheritsBaseEntity)
                {
                    failures.Add($"{entityType.FullName} должен реализовывать IEntity<T> или наследоваться от BaseEntity<T>");
                }
            }
        }

        failures.ShouldBeEmpty(
            $"Сущности в пространстве имён Core должны реализовывать IEntity<T> или наследоваться от BaseEntity<T>. " +
            $"Нарушения: {string.Join(", ", failures)}");
    }

    [Fact]
    public void Aggregate_Roots_Should_Not_Reference_Other_Aggregates_Directly()
    {
        // Это мягкая проверка — корни агрегатов должны ссылаться на другие агрегаты только по ID.
        // Проверяем, что свойства агрегата не раскрывают напрямую типы других агрегатов.
        var failures = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            var aggregateTypes = module.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => IsSubclassOfGeneric(t, typeof(AggregateRoot<>)));

            foreach (var aggregateType in aggregateTypes)
            {
                // Получаем все публичные свойства
                var properties = aggregateType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var property in properties)
                {
                    var propertyType = property.PropertyType;

                    // Пропускаем типы коллекций и проверяем тип элемента
                    if (propertyType.IsGenericType)
                    {
                        var genericArgs = propertyType.GetGenericArguments();
                        if (genericArgs.Length > 0)
                        {
                            propertyType = genericArgs[0];
                        }
                    }

                    // Проверяем, является ли тип свойства другим корнем агрегата (исключая ссылки на себя)
                    if (propertyType != aggregateType &&
                        IsSubclassOfGeneric(propertyType, typeof(AggregateRoot<>)))
                    {
                        failures.Add(
                            $"{aggregateType.Name}.{property.Name} напрямую ссылается на агрегат {propertyType.Name}. " +
                            "Рассмотрите возможность ссылки по ID.");
                    }
                }
            }
        }

        failures.ShouldBeEmpty(
            $"Корни агрегатов не должны напрямую ссылаться на другие корни агрегатов — используйте ссылки по ID. " +
            $"Нарушения: {string.Join(", ", failures)}");
    }

    [Fact]
    public void Value_Objects_Should_Be_Immutable()
    {
        var failures = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            var valueObjectTypes = module.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.Name.EndsWith("ValueObject", StringComparison.Ordinal)
                         || t.BaseType?.Name == "ValueObject");

            foreach (var voType in valueObjectTypes)
            {
                // Проверяем, что все публичные свойства не имеют публичного сеттера
                var mutableProperties = voType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.SetMethod != null && p.SetMethod.IsPublic)
                    .ToArray();

                if (mutableProperties.Length > 0)
                {
                    failures.Add(
                        $"{voType.FullName} содержит изменяемые свойства: " +
                        $"{string.Join(", ", mutableProperties.Select(p => p.Name))}");
                }
            }
        }

        failures.ShouldBeEmpty(
            $"Объекты-значения должны быть неизменяемыми (без публичных сеттеров). " +
            $"Нарушения: {string.Join(", ", failures)}");
    }

    private static bool IsSubclassOfGeneric(Type type, Type genericBase)
    {
        while (type != null && type != typeof(object))
        {
            var current = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            if (genericBase == current)
            {
                return true;
            }
            type = type.BaseType!;
        }
        return false;
    }
}
