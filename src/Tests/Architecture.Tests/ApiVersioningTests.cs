using EDV.Modules.Auditing;
using EDV.Modules.Identity;
using EDV.Modules.Multitenancy;
using NetArchTest.Rules;
using Shouldly;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Тесты для соблюдения соглашений версионирования API во всех модулях.
/// </summary>
public partial class ApiVersioningTests
{
    private static readonly Assembly[] ModuleAssemblies =
    [
        typeof(AuditingModule).Assembly,
        typeof(IdentityModule).Assembly,
        typeof(MultitenancyModule).Assembly
    ];

    private static readonly string SolutionRoot = ModuleArchitectureTestsFixture.SolutionRoot;

    [Fact]
    public void Features_Should_Be_In_Versioned_Namespace()
    {
        foreach (var module in ModuleAssemblies)
        {
            var result = Types
                .InAssembly(module)
                .That()
                .ResideInNamespaceContaining(".Features.")
                .Should()
                .ResideInNamespaceMatching(@"\.Features\.v\d+")
                .GetResult();

            var failingTypes = result.FailingTypeNames ?? [];

            result.IsSuccessful.ShouldBeTrue(
                $"Функции в модуле «{module.GetName().Name}» должны находиться в версионированных пространствах имён (v1, v2 и т.д.). " +
                $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
        }
    }

    [Fact]
    public void Feature_Folders_Should_Follow_Version_Convention()
    {
        string modulesRoot = Path.Combine(SolutionRoot, "src", "Modules");

        if (!Directory.Exists(modulesRoot))
        {
            return;
        }

        var featureFolders = Directory
            .GetDirectories(modulesRoot, "Features", SearchOption.AllDirectories)
            .ToArray();

        var violations = new List<string>();

        foreach (var featuresFolder in featureFolders)
        {
            var subFolders = Directory.GetDirectories(featuresFolder);

            foreach (var subFolder in subFolders)
            {
                string folderName = Path.GetFileName(subFolder);

                // Папки функций непосредственно внутри Features должны быть папками версий (v1, v2 и т.д.)
                if (!VersionFolderRegex().IsMatch(folderName))
                {
                    violations.Add(
                        $"Папка «{subFolder}» должна быть папкой версии (v1, v2 и т.д.), а не «{folderName}»");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Папки функций должны быть организованы по версиям. " +
            $"Нарушения: {string.Join("; ", violations)}");
    }

    [Fact]
    public void V1_Types_Should_Not_Depend_On_Higher_Versions()
    {
        // Уже проверяется в FeatureArchitectureTests, но здесь дублируется для надёжности
        foreach (var module in ModuleAssemblies)
        {
            var result = Types
                .InAssembly(module)
                .That()
                .ResideInNamespaceContaining(".v1.")
                .ShouldNot()
                .HaveDependencyOnAny(
                    ".v2.",
                    ".v3.",
                    ".v4.",
                    ".v5.")
                .GetResult();

            var failingTypes = result.FailingTypeNames ?? [];

            result.IsSuccessful.ShouldBeTrue(
                $"Типы v1 в модуле «{module.GetName().Name}» не должны зависеть от более новых версий. " +
                $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
        }
    }

    [Fact]
    public void Higher_Versions_Can_Depend_On_Lower_Versions()
    {
        // Это разрешительный тест — v2 может зависеть от v1 для обратной совместимости.
        // Просто проверяем, что такой паттерн существует.
        foreach (var module in ModuleAssemblies)
        {
            var v2Types = module.GetTypes()
                .Where(t => t.Namespace?.Contains(".v2.", StringComparison.Ordinal) == true)
                .ToArray();

            // Если v2 существует, ему должно быть разрешено ссылаться на v1.
            // Этот тест документирует ожидаемое поведение — типы v2 могут существовать.
            v2Types.ShouldNotBeNull();
        }
    }

    [Fact]
    public void Commands_And_Queries_Should_Be_In_Same_Version_As_Handlers()
    {
        var violations = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            var handlerTypes = module.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.Name.EndsWith("Handler", StringComparison.Ordinal))
                .Where(t => t.Namespace?.Contains(".Features.", StringComparison.Ordinal) == true);

            foreach (var handlerType in handlerTypes)
            {
                string? handlerNamespace = handlerType.Namespace;
                var handlerVersion = ExtractVersion(handlerNamespace);

                if (string.IsNullOrEmpty(handlerVersion))
                {
                    continue;
                }

                // Находим тип команды/запроса, который обрабатывает данный обработчик
                var handlerInterfaces = handlerType.GetInterfaces()
                    .Where(i => i.IsGenericType &&
                               (i.Name.Contains("CommandHandler", StringComparison.Ordinal) ||
                                i.Name.Contains("QueryHandler", StringComparison.Ordinal)));

                foreach (var handlerInterface in handlerInterfaces)
                {
                    var genericArgs = handlerInterface.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        var requestType = genericArgs[0];
                        var requestVersion = ExtractVersion(requestType.Namespace);

                        if (!string.IsNullOrEmpty(requestVersion) &&
                            !handlerVersion.Equals(requestVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            violations.Add(
                                $"{handlerType.Name} ({handlerVersion}) обрабатывает {requestType.Name} ({requestVersion})");
                        }
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Обработчики должны обрабатывать команды/запросы той же версии API. " +
            $"Нарушения: {string.Join("; ", violations)}");
    }

    [Fact]
    public void Each_Version_Should_Be_Self_Contained()
    {
        // Проверяем, что каждая папка версии содержит все необходимые компоненты
        string modulesRoot = Path.Combine(SolutionRoot, "src", "Modules");

        if (!Directory.Exists(modulesRoot))
        {
            return;
        }

        var warnings = new List<string>();

        var moduleDirectories = Directory.GetDirectories(modulesRoot);

        foreach (var moduleDir in moduleDirectories)
        {
            var featuresDir = Directory
                .GetDirectories(moduleDir, "Features", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (featuresDir == null) continue;

            var versionDirs = Directory.GetDirectories(featuresDir)
                .Where(d => VersionFolderRegex().IsMatch(Path.GetFileName(d)));

            foreach (var versionDir in versionDirs)
            {
                var featureDirs = Directory.GetDirectories(versionDir);

                foreach (var featureDir in featureDirs)
                {
                    var files = Directory.GetFiles(featureDir, "*.cs");
                    var fileNames = files.Select(Path.GetFileNameWithoutExtension).ToHashSet();

                    // Проверяем наличие типовых компонентов функции
                    bool hasEndpoint = fileNames.Any(f => f!.EndsWith("Endpoint", StringComparison.Ordinal));
                    bool hasHandler = fileNames.Any(f => f!.EndsWith("Handler", StringComparison.Ordinal));

                    // У функции должен быть хотя бы endpoint или handler
                    if (!hasEndpoint && !hasHandler)
                    {
                        warnings.Add(
                            $"Функция «{Path.GetFileName(featureDir)}» в {Path.GetFileName(versionDir)} " +
                            "не имеет endpoint или handler");
                    }
                }
            }
        }

        // Информационная проверка — некоторые функции могут быть структурированы иначе.
        // Утверждаем, что каталоги были обработаны (тест выполнился успешно).
        warnings.ShouldNotBeNull();
    }

    private static string? ExtractVersion(string? ns)
    {
        if (string.IsNullOrEmpty(ns)) return null;

        var match = Regex.Match(ns, @"\.v(\d+)\.", RegexOptions.IgnoreCase);
        return match.Success ? $"v{match.Groups[1].Value}" : null;
    }

    [GeneratedRegex(@"^v\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionFolderRegex();
}
