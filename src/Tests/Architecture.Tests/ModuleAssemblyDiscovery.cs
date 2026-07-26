using Shouldly;
using System.Reflection;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Обнаруживает все сборки модулей EDV для использования в архитектурных тестах.
/// Использует список исходных сборок для гарантии загрузки нужного AppDomain,
/// затем автоматически обнаруживает все дополнительные загруженные сборки модулей.
/// Для добавления нового модуля достаточно добавить ссылку на его сборку в проект
/// Architecture.Tests — изменения в этом файле не требуются.
/// </summary>
internal static class ModuleAssemblyDiscovery
{
    private static readonly Assembly[] _cached = Discover();

    /// <summary>
    /// Возвращает все загруженные сборки модулей EDV (исключая сборки Contracts).
    /// </summary>
    public static Assembly[] GetModuleAssemblies() => _cached;

    private static Assembly[] Discover()
    {
        // Получаем каталог, в котором выполняются тесты
        string baseDir = AppContext.BaseDirectory;

        // Ищем файлы EDV.Modules.*.dll (исключая Contracts)
        var moduleFiles = Directory.GetFiles(baseDir, "EDV.Modules.*.dll")
            .Where(f => !f.EndsWith(".Contracts.dll", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var assemblies = new List<Assembly>();

        foreach (var file in moduleFiles)
        {
            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(file);
                assemblies.Add(Assembly.Load(assemblyName));
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
            {
                // Пропускаем, если это не валидная сборка .NET или произошла иная ошибка загрузки
            }
#pragma warning restore CA1031
        }

        return assemblies
            .OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
            .ToArray();
    }
}

/// <summary>
/// Проверка, гарантирующая, что была обнаружена хотя бы одна сборка модуля.
/// Предотвращает тихий no-op, если все ссылки на модули были случайно удалены.
/// </summary>
public sealed class ModuleAssemblyDiscoveryGuardTests
{
    [Fact]
    public void ModuleAssemblyDiscovery_Should_FindAtLeastOneModule()
    {
        var assemblies = ModuleAssemblyDiscovery.GetModuleAssemblies();

        if (assemblies.Length == 0)
        {
            throw new InvalidOperationException(
                "ModuleAssemblyDiscovery не нашёл ни одной сборки модуля EDV. " +
                "Убедитесь, что Architecture.Tests.csproj ссылается хотя бы на один проект Modules.*.");
        }

        assemblies.ShouldNotBeEmpty();
    }
}
