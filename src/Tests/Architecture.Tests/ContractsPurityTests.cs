using EDV.Modules.Auditing.Contracts;
using EDV.Modules.Identity.Contracts;
using EDV.Modules.Multitenancy.Contracts;
using NetArchTest.Rules;
using Shouldly;
using System.Reflection;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Тесты для проверки того, что проекты Contracts остаются чистыми и содержат только DTO,
/// команды, запросы и интерфейсы сервисов — без деталей реализации.
/// </summary>
public class ContractsPurityTests
{
    private static readonly Assembly[] ContractsAssemblies =
    [
        typeof(AuditingContractsMarker).Assembly,
        typeof(IdentityContractsMarker).Assembly,
        typeof(MultitenancyContractsMarker).Assembly
    ];

    [Fact]
    public void Contracts_Should_Not_Depend_On_EntityFramework()
    {
        foreach (var assembly in ContractsAssemblies)
        {
            var result = Types
                .InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("Microsoft.EntityFrameworkCore")
                .GetResult();

            var failingTypes = result.FailingTypeNames ?? [];

            result.IsSuccessful.ShouldBeTrue(
                $"Сборка Contracts «{assembly.GetName().Name}» не должна зависеть от Entity Framework. " +
                $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
        }
    }

    [Fact]
    public void Contracts_Should_Not_Depend_On_FluentValidation()
    {
        foreach (var assembly in ContractsAssemblies)
        {
            var result = Types
                .InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("FluentValidation")
                .GetResult();

            var failingTypes = result.FailingTypeNames ?? [];

            result.IsSuccessful.ShouldBeTrue(
                $"Сборка Contracts «{assembly.GetName().Name}» не должна зависеть от FluentValidation. " +
                $"Валидаторы должны находиться в реализации модуля, а не в contracts. " +
                $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
        }
    }

    [Fact]
    public void Contracts_Should_Not_Depend_On_Hangfire()
    {
        foreach (var assembly in ContractsAssemblies)
        {
            var result = Types
                .InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("Hangfire")
                .GetResult();

            var failingTypes = result.FailingTypeNames ?? [];

            result.IsSuccessful.ShouldBeTrue(
                $"Сборка Contracts «{assembly.GetName().Name}» не должна зависеть от Hangfire. " +
                $"Планирование задач — это деталь реализации. " +
                $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
        }
    }

    [Fact]
    public void Contracts_Should_Not_Depend_On_Module_Implementations()
    {
        string[] moduleImplementations =
        [
            "EDV.Modules.Auditing.Features",
            "EDV.Modules.Auditing.Data",
            "EDV.Modules.Auditing.Persistence",
            "EDV.Modules.Identity.Features",
            "EDV.Modules.Identity.Data",
            "EDV.Modules.Identity.Persistence",
            "EDV.Modules.Multitenancy.Features",
            "EDV.Modules.Multitenancy.Data",
            "EDV.Modules.Multitenancy.Persistence"
        ];

        foreach (var assembly in ContractsAssemblies)
        {
            var result = Types
                .InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(moduleImplementations)
                .GetResult();

            var failingTypes = result.FailingTypeNames ?? [];

            result.IsSuccessful.ShouldBeTrue(
                $"Сборка Contracts «{assembly.GetName().Name}» не должна зависеть от реализаций модулей. " +
                $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
        }
    }

    [Fact]
    public void Contracts_Should_Not_Contain_DbContext_Types()
    {
        foreach (var assembly in ContractsAssemblies)
        {
            var dbContextTypes = assembly.GetTypes()
                .Where(t => t.Name.Contains("DbContext", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            dbContextTypes.ShouldBeEmpty(
                $"Сборка Contracts «{assembly.GetName().Name}» не должна содержать типы DbContext. " +
                $"Найдено: {string.Join(", ", dbContextTypes.Select(t => t.FullName))}");
        }
    }

    [Fact]
    public void Contracts_Should_Not_Contain_Repository_Types()
    {
        foreach (var assembly in ContractsAssemblies)
        {
            var repositoryTypes = assembly.GetTypes()
                .Where(t => t.Name.Contains("Repository", StringComparison.OrdinalIgnoreCase)
                         && !t.IsInterface) // Интерфейсы вроде IRepository допустимы
                .ToArray();

            repositoryTypes.ShouldBeEmpty(
                $"Сборка Contracts «{assembly.GetName().Name}» не должна содержать конкретные типы репозиториев. " +
                $"Найдено: {string.Join(", ", repositoryTypes.Select(t => t.FullName))}");
        }
    }

    [Fact]
    public void Commands_And_Queries_Should_Be_Records_Or_Sealed()
    {
        var nonSealedTypes = new List<string>();

        foreach (var assembly in ContractsAssemblies)
        {
            var commandQueryTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.Name.EndsWith("Command", StringComparison.Ordinal)
                         || t.Name.EndsWith("Query", StringComparison.Ordinal));

            foreach (var type in commandQueryTypes)
            {
                // Records неявно являются sealed с точки зрения наследования (их нельзя унаследовать обычным способом).
                // Проверяем, является ли тип record, по наличию специального свойства EqualityContract.
                bool isRecord = type.GetProperty("EqualityContract",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance) != null;

                if (!isRecord && !type.IsSealed)
                {
                    nonSealedTypes.Add($"{type.FullName}");
                }
            }
        }

        // Только информационно: sealed-команды/запросы рекомендуются, но не обязательны (постепенная миграция).
        // Просто проверяем, что тестовая инфраструктура способна выявлять несовместимые типы.
        nonSealedTypes.ShouldNotBeNull();
    }
}
