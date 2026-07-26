using EDV.Modules.Auditing;
using EDV.Modules.Identity;
using EDV.Modules.Multitenancy;
using System.Reflection;

namespace Generic.Tests.Architecture;

/// <summary>
/// Обнаруживает все сборки модулей для использования в общих архитектурных тестах.
/// </summary>
internal static class ModuleAssemblyDiscovery
{
    private static readonly Assembly[] _cached = Discover();

    public static Assembly[] GetModuleAssemblies() => _cached;

    private static Assembly[] Discover()
    {
        // Принудительно загружаем исходные сборки
        _ = typeof(AuditingModule);
        _ = typeof(IdentityModule);
        _ = typeof(MultitenancyModule);

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a =>
            {
                var name = a.GetName().Name ?? string.Empty;
                return name.StartsWith("EDV.Modules.", StringComparison.Ordinal)
                       && !name.EndsWith(".Contracts", StringComparison.Ordinal);
            })
            .OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
            .ToArray();
    }
}
