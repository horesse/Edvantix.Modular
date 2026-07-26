using EDV.Framework.Shared.Identity.Authorization;
using Shouldly;
using System.Reflection;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Защищает контракт метаданных прав доступа между <c>RequiredPermissionAttribute</c> и
/// <c>RequiredPermissionAuthorizationHandler</c>. Обработчик определяет разрешения endpoint через
/// <see cref="IRequiredPermissionMetadata"/>; если дублирующий <c>RequiredPermissionAttribute</c>
/// появится в другой сборке без реализации этого интерфейса, endpoint-ы, помеченные им,
/// не будут иметь распознаваемых метаданных, и каждый gate <c>.RequirePermission()</c> молча перестанет срабатывать.
/// </summary>
public class AuthorizationMetadataTests
{
    private const string AttributeName = "RequiredPermissionAttribute";
    private const string ExpectedNamespace = "EDV.Framework.Shared.Identity.Authorization";

    [Fact]
    public void RequiredPermissionAttribute_Should_Exist_Exactly_Once_Across_All_EDV_Assemblies()
    {
        var matches = GetAllAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(t => string.Equals(t.Name, AttributeName, StringComparison.Ordinal))
            .ToArray();

        matches.ShouldNotBeEmpty(
            $"{AttributeName} не найден ни в одной сборке EDV. " +
            "От него зависит конвейер авторизации по правам доступа.");

        matches.Length.ShouldBe(1,
            $"Во всех сборках EDV должен существовать ровно один {AttributeName}. " +
            "Дубликат, не реализующий IRequiredPermissionMetadata, молча отключает " +
            $"каждый gate .RequirePermission(). Найдено: {string.Join(", ", matches.Select(t => $"{t.FullName} ({t.Assembly.GetName().Name})"))}");

        matches[0].Namespace.ShouldBe(ExpectedNamespace,
            $"{AttributeName} должен находиться в {ExpectedNamespace} — именно оттуда " +
            "RequiredPermissionAuthorizationHandler получает его метаданные.");
    }

    [Fact]
    public void RequiredPermissionAttribute_Should_Implement_IRequiredPermissionMetadata()
    {
        var attributeType = typeof(RequiredPermissionAttribute);

        typeof(IRequiredPermissionMetadata).IsAssignableFrom(attributeType).ShouldBeTrue(
            $"{attributeType.FullName} должен реализовывать IRequiredPermissionMetadata. " +
            "RequiredPermissionAuthorizationHandler определяет разрешения endpoint через этот " +
            "интерфейс; без него каждый gate .RequirePermission() молча перестанет срабатывать.");
    }

    /// <summary>
    /// Загружает каждую сборку EDV.*, поставляемую вместе с тестами, чтобы проверка на дубликаты
    /// охватывала BuildingBlocks, все модули (включая Contracts) и сборки хостов — а не только
    /// runtime-сборки модулей, которые возвращает ModuleAssemblyDiscovery.
    /// </summary>
    private static Assembly[] GetAllAssemblies()
    {
        string baseDir = AppContext.BaseDirectory;

        var assemblies = new List<Assembly>();

        foreach (var file in Directory.GetFiles(baseDir, "EDV.*.dll"))
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

        assemblies.ShouldNotBeEmpty(
            "В выходном каталоге тестов не найдено ни одной сборки EDV.*; проверка на дубликаты не имела бы смысла.");

        return [.. assemblies];
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
