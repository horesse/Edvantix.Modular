using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Architecture.Tests;

public class HostArchitectureTests
{
    [Fact]
    public void Modules_Should_Not_Depend_On_Hosts()
    {
        // Сборки / пространства имён, представляющие хост-приложения.
        string[] hostNamespaces =
        {
            "EDV.Starter.Api"
        };

        var result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace("EDV.Modules")
            .Should()
            .NotHaveDependencyOnAny(hostNamespaces)
            .GetResult();

        var failingTypes = result.FailingTypeNames ?? Array.Empty<string>();

        result.IsSuccessful.ShouldBeTrue(
            "Код модулей не должен зависеть от сборок хостов. " +
            $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
    }

    [Fact]
    public void Hosts_Should_Not_Depend_On_Module_Internals()
    {
        // Хосты могут зависеть от contracts модулей и корневых типов модулей,
        // но не должны напрямую ссылаться на пространства имён features или слоя данных.
        string[] forbiddenNamespaces =
        {
            "EDV.Modules.Auditing.Features",
            "EDV.Modules.Auditing.Data",
            "EDV.Modules.Identity.Features",
            "EDV.Modules.Identity.Data",
            "EDV.Modules.Multitenancy.Features",
            "EDV.Modules.Multitenancy.Data"
        };

        var hostResult = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace("EDV.Starter")
            .Should()
            .NotHaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        var hostFailingTypes = hostResult.FailingTypeNames ?? Array.Empty<string>();

        hostResult.IsSuccessful.ShouldBeTrue(
            "Хосты не должны напрямую зависеть от внутренних деталей features или data модулей. " +
            $"Несоответствующие типы: {string.Join(", ", hostFailingTypes)}");
    }
}

internal static class ModuleArchitectureTestsFixture
{
    public static readonly string SolutionRoot = GetSolutionRoot();

    private static string GetSolutionRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Не удалось найти корень решения, содержащий папку 'src'.");
        }

        return directory.FullName;
    }
}
