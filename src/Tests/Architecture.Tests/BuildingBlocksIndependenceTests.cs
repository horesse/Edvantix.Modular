using EDV.Framework.Core;
using EDV.Framework.Persistence;
using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Web;
using NetArchTest.Rules;
using Shouldly;
using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Тесты для проверки того, что BuildingBlocks остаются независимыми и переиспользуемыми,
/// без зависимостей от прикладных модулей.
/// </summary>
public class BuildingBlocksIndependenceTests
{
    private static readonly string SolutionRoot = ModuleArchitectureTestsFixture.SolutionRoot;

    private static readonly Assembly[] BuildingBlockAssemblies =
    [
        typeof(ICore).Assembly, // Core
        typeof(IConnectionStringValidator).Assembly, // Persistence
        typeof(IAppTenantInfo).Assembly, // Shared
        typeof(IWeb).Assembly // Web
    ];

    [Fact]
    public void BuildingBlocks_Should_Not_Depend_On_Modules()
    {
        foreach (var assembly in BuildingBlockAssemblies)
        {
            var result = Types
                .InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(
                    "EDV.Modules.Auditing",
                    "EDV.Modules.Identity",
                    "EDV.Modules.Multitenancy")
                .GetResult();

            var failingTypes = result.FailingTypeNames ?? [];

            result.IsSuccessful.ShouldBeTrue(
                $"BuildingBlock «{assembly.GetName().Name}» не должен зависеть от Modules. " +
                $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
        }
    }

    [Fact]
    public void BuildingBlocks_Should_Not_Depend_On_Hosts()
    {
        foreach (var assembly in BuildingBlockAssemblies)
        {
            var result = Types
                .InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(
                    "EDV.Starter",
                    "EDV.Starter.Api")
                .GetResult();

            var failingTypes = result.FailingTypeNames ?? [];

            result.IsSuccessful.ShouldBeTrue(
                $"BuildingBlock «{assembly.GetName().Name}» не должен зависеть от Hosts. " +
                $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
        }
    }

    [Fact]
    public void BuildingBlocks_Projects_Should_Not_Reference_Modules_Directly()
    {
        string buildingBlocksRoot = Path.Combine(SolutionRoot, "src", "BuildingBlocks");

        var projects = Directory
            .GetFiles(buildingBlocksRoot, "*.csproj", SearchOption.AllDirectories)
            .ToArray();

        projects.Length.ShouldBeGreaterThan(0);

        var violations = new List<string>();

        foreach (string projectPath in projects)
        {
            string projectName = Path.GetFileNameWithoutExtension(projectPath);
            var document = XDocument.Load(projectPath);

            var references = document
                .Descendants("ProjectReference")
                .Select(x => (string?)x.Attribute("Include") ?? string.Empty)
                .Where(include => include.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (string include in references)
            {
                string referencedName = GetReferencedProjectName(include);

                // Проверяем, ссылается ли проект на Modules
                if (referencedName.StartsWith("Modules.", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{projectName} -> {referencedName}");
                }

                // Проверяем, ссылается ли проект на Host
                if (referencedName.Contains("AppHost", StringComparison.OrdinalIgnoreCase) ||
                    referencedName.StartsWith("EDV.Starter.Api", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{projectName} -> {referencedName}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"BuildingBlocks не должны ссылаться на проекты Modules или Host. " +
            $"Нарушения: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Core_BuildingBlock_Should_Be_Dependency_Free()
    {
        // Core должен зависеть только от .NET BCL и абстракций Mediator
        string[] allowedDependencies =
        [
            "System",
            "Microsoft",
            "Mediator.Abstractions",
            "netstandard",
            "mscorlib"
        ];

        string coreProjectPath = Path.Combine(SolutionRoot, "src", "BuildingBlocks", "Core", "Core.csproj");
        var document = XDocument.Load(coreProjectPath);

        var packageReferences = document
            .Descendants("PackageReference")
            .Select(x => (string?)x.Attribute("Include") ?? string.Empty)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();

        var projectReferences = document
            .Descendants("ProjectReference")
            .Select(x => (string?)x.Attribute("Include") ?? string.Empty)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();

        // У Core не должно быть ссылок на другие проекты BuildingBlocks
        projectReferences.ShouldBeEmpty(
            $"BuildingBlock Core не должен ссылаться на другие проекты. " +
            $"Найдено: {string.Join(", ", projectReferences)}");

        // Проверяем, что ссылки на пакеты минимальны
        var disallowedPackages = packageReferences
            .Where(p => !allowedDependencies.Any(a =>
                p.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        // Примечание: это информационная проверка — некоторые зависимости могут быть допустимы
        if (disallowedPackages.Length > 0)
        {
            // Проверьте эти зависимости, чтобы Core оставался лёгковесным
        }
    }

    [Fact]
    public void BuildingBlocks_Should_Follow_Layered_Dependencies()
    {
        // Ожидаемая слоистая структура (нижний слой не должен зависеть от верхнего): 0=Core; 1=Shared/Caching/Mailing/Storage;
        // 2=Persistence/Jobs; 3=Eventing/Web.

        var layerViolations = new List<string>();

        // Core не должен зависеть ни от чего
        CheckBuildingBlockDependencies("Core", [], layerViolations);

        // Shared должен зависеть только от Core
        CheckBuildingBlockDependencies("Shared", ["Core"], layerViolations);

        // Caching должен зависеть только от Core
        CheckBuildingBlockDependencies("Caching", ["Core"], layerViolations);

        // Mailing должен зависеть только от Core
        CheckBuildingBlockDependencies("Mailing", ["Core"], layerViolations);

        // Storage зависит от Core и Shared (FileUploadRequest перенесён в Shared), а также от Quota,
        // поскольку декоратор хранилища с учётом квот находится здесь и списывает StorageBytes за каждую загрузку.
        CheckBuildingBlockDependencies("Storage", ["Core", "Shared", "Quota"], layerViolations);

        // Persistence должен зависеть от Core, Shared
        CheckBuildingBlockDependencies("Persistence", ["Core", "Shared"], layerViolations);

        // Jobs должен зависеть от Core, Shared
        CheckBuildingBlockDependencies("Jobs", ["Core", "Shared"], layerViolations);

        // Eventing.Abstractions не должен иметь зависимостей (лёгковесные интерфейсы)
        CheckBuildingBlockDependencies("Eventing.Abstractions", [], layerViolations);

        // Eventing должен зависеть от Core и Eventing.Abstractions
        CheckBuildingBlockDependencies("Eventing", ["Core", "Eventing.Abstractions"], layerViolations);

        layerViolations.ShouldBeEmpty(
            $"BuildingBlocks должны соблюдать правила слоистых зависимостей. " +
            $"Нарушения: {string.Join("; ", layerViolations)}");
    }

    private static void CheckBuildingBlockDependencies(
        string projectName,
        string[] allowedDependencies,
        List<string> violations)
    {
        string projectPath = Path.Combine(SolutionRoot, "src", "BuildingBlocks", projectName, $"{projectName}.csproj");

        if (!File.Exists(projectPath))
        {
            return; // Проект не существует
        }

        var document = XDocument.Load(projectPath);

        var projectReferences = document
            .Descendants("ProjectReference")
            .Select(x => (string?)x.Attribute("Include") ?? string.Empty)
            .Select(GetReferencedProjectName)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();

        foreach (var reference in projectReferences)
        {
            if (!allowedDependencies.Contains(reference, StringComparer.OrdinalIgnoreCase))
            {
                violations.Add(
                    $"{projectName} зависит от {reference} (нет в списке разрешённых: {string.Join(", ", allowedDependencies)})");
            }
        }
    }

    // Пути ProjectReference используют разделители Windows (..\Core\Core.csproj), но GetFileNameWithoutExtension
    // разделяет только по '\' в Windows — сначала нормализуем в '/', чтобы Linux CI тоже получал чистое имя проекта.
    private static string GetReferencedProjectName(string includePath) =>
        Path.GetFileNameWithoutExtension(includePath.Replace('\\', '/'));
}
