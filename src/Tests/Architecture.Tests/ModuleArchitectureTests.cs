using Shouldly;
using System.Xml.Linq;
using Xunit;

namespace Architecture.Tests;

public class ModuleArchitectureTests
{
    [Fact]
    public void Modules_Should_Not_Depend_On_Other_Modules()
    {
        string solutionRoot = GetSolutionRoot();
        string modulesRoot = Path.Combine(solutionRoot, "src", "Modules");

        var runtimeProjects = Directory
            .GetFiles(modulesRoot, "Modules.*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains(".Contracts", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        runtimeProjects.Length.ShouldBeGreaterThan(0);

        foreach (string projectPath in runtimeProjects)
        {
            string currentName = Path.GetFileNameWithoutExtension(projectPath);

            var document = XDocument.Load(projectPath);
            var references = document
                .Descendants("ProjectReference")
                .Select(x => (string?)x.Attribute("Include") ?? string.Empty)
                .Where(include => include.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (string include in references)
            {
                string referencedName = Path.GetFileNameWithoutExtension(include);

                bool isModuleRuntime = referencedName.StartsWith("Modules.", StringComparison.OrdinalIgnoreCase)
                                       && !referencedName.EndsWith(".Contracts", StringComparison.OrdinalIgnoreCase);

                if (!isModuleRuntime)
                {
                    continue;
                }

                bool isSelfReference = string.Equals(referencedName, currentName, StringComparison.OrdinalIgnoreCase);
                isSelfReference.ShouldBeTrue(
                    $"Runtime-проект модуля «{currentName}» не должен ссылаться на runtime-проект другого модуля «{referencedName}». " +
                    "Разрешены только проекты contracts или building block.");
            }
        }
    }

    private static string GetSolutionRoot()
    {
        // Начинаем с текущего каталога и поднимаемся вверх, пока не найдём `src`.
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
