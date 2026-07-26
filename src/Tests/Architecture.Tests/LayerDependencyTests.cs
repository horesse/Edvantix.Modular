using EDV.Framework.Core;
using NetArchTest.Rules;
using Shouldly;
using System.Reflection;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Тесты для соблюдения потока слоистых зависимостей:
/// Domain → Application → Infrastructure → Presentation
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly[] ModuleAssemblies = ModuleAssemblyDiscovery.GetModuleAssemblies();

    private static readonly Assembly CoreAssembly = typeof(ICore).Assembly;

    [Fact]
    public void Core_Should_Not_Depend_On_EntityFramework()
    {
        var result = Types
            .InAssembly(CoreAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        var failingTypes = result.FailingTypeNames ?? [];

        result.IsSuccessful.ShouldBeTrue(
            $"BuildingBlocks.Core не должен зависеть от Entity Framework. " +
            $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
    }

    [Fact]
    public void Core_Should_Not_Depend_On_AspNetCore()
    {
        var result = Types
            .InAssembly(CoreAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.AspNetCore",
                "Microsoft.AspNetCore.Http")
            .GetResult();

        var failingTypes = result.FailingTypeNames ?? [];

        result.IsSuccessful.ShouldBeTrue(
            $"BuildingBlocks.Core не должен зависеть от ASP.NET Core. " +
            $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
    }

    [Fact]
    public void Domain_Types_Should_Not_Depend_On_Persistence()
    {
        foreach (var module in ModuleAssemblies)
        {
            var result = Types
                .InAssembly(module)
                .That()
                .ResideInNamespaceContaining(".Core.")
                .ShouldNot()
                .HaveDependencyOnAny(
                    ".Persistence.",
                    ".Data.",
                    "Microsoft.EntityFrameworkCore")
                .GetResult();

            var failingTypes = result.FailingTypeNames ?? [];

            result.IsSuccessful.ShouldBeTrue(
                $"Доменные типы в модуле «{module.GetName().Name}» не должны зависеть от слоя персистентности. " +
                $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
        }
    }

    [Fact]
    public void Domain_Types_Should_Not_Depend_On_Infrastructure()
    {
        foreach (var module in ModuleAssemblies)
        {
            var result = Types
                .InAssembly(module)
                .That()
                .ResideInNamespaceContaining(".Core.")
                .ShouldNot()
                .HaveDependencyOnAny(
                    ".Infrastructure.",
                    ".Services.",
                    "Microsoft.Extensions.Logging",
                    "Microsoft.Extensions.Options")
                .GetResult();

            var failingTypes = result.FailingTypeNames ?? [];

            result.IsSuccessful.ShouldBeTrue(
                $"Доменные типы в модуле «{module.GetName().Name}» не должны зависеть от инфраструктуры. " +
                $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
        }
    }

    [Fact]
    public void Features_Should_Not_Depend_On_AspNetCore_Directly()
    {
        // Функции должны использовать абстракции Minimal API из BuildingBlocks.Web,
        // а не зависеть напрямую от внутренних деталей ASP.NET Core
        foreach (var module in ModuleAssemblies)
        {
            var result = Types
                .InAssembly(module)
                .That()
                .ResideInNamespaceContaining(".Features.")
                .And()
                .DoNotHaveNameEndingWith("Endpoint")
                .ShouldNot()
                .HaveDependencyOnAny(
                    "Microsoft.AspNetCore.Http.HttpContext",
                    "Microsoft.AspNetCore.Mvc")
                .GetResult();

            var failingTypes = result.FailingTypeNames ?? [];

            result.IsSuccessful.ShouldBeTrue(
                $"Обработчики/валидаторы функций в модуле «{module.GetName().Name}» не должны напрямую зависеть от ASP.NET Core. " +
                $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
        }
    }
}
