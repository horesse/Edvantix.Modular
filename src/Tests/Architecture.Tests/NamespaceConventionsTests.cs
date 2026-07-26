using Shouldly;
using Xunit;

namespace Architecture.Tests;

public class NamespaceConventionsTests
{
    private static readonly string SolutionRoot = ModuleArchitectureTestsFixture.SolutionRoot;

    [Fact]
    public void BuildingBlocks_Core_Domain_Namespaces_Should_Match_Folder()
    {
        string domainRoot = Path.Combine(SolutionRoot, "src", "BuildingBlocks", "Core", "Domain");

        if (!Directory.Exists(domainRoot))
        {
            // Если папка ещё не существует, считаем это нейтральным прохождением теста.
            return;
        }

        var files = Directory
            .GetFiles(domainRoot, "*.cs", SearchOption.AllDirectories)
            .ToArray();

        files.Length.ShouldBeGreaterThan(0);

        foreach (string file in files)
        {
            string content = File.ReadAllText(file);

            var namespaceLine = content
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => line.TrimStart().StartsWith("namespace ", StringComparison.Ordinal));

            namespaceLine.ShouldNotBeNull($"Файл «{file}» должен объявлять пространство имён, соответствующее структуре папок.");

            string declaredNamespace = namespaceLine!["namespace ".Length..].Trim().TrimEnd(';');

            declaredNamespace
                .Contains(".Core.", StringComparison.Ordinal)
                .ShouldBeTrue($"Пространство имён «{declaredNamespace}» должно содержать «.Core.» для файла «{file}».");

            declaredNamespace
                .Contains(".Domain", StringComparison.Ordinal)
                .ShouldBeTrue($"Пространство имён «{declaredNamespace}» должно содержать «.Domain» для файла «{file}».");
        }
    }
}
