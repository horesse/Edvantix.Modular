using EDV.Modules.Auditing;
using EDV.Modules.Identity;
using EDV.Modules.Multitenancy;
using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Architecture.Tests;

public class FeatureArchitectureTests
{
    [Fact]
    public void Features_Versions_Should_Not_Depend_On_Newer_Versions()
    {
        // Защита для будущих версий (v2, v3, ...). Пока что это в основном
        // подстраховка от случайного связывания функций разных версий.
        var modules = new[]
        {
            typeof(AuditingModule).Assembly,
            typeof(IdentityModule).Assembly,
            typeof(MultitenancyModule).Assembly
        };

        foreach (var module in modules)
        {
            var v1Result = Types
                .InAssembly(module)
                .That()
                .ResideInNamespaceEndingWith(".Features.v1")
                .Should()
                .NotHaveDependencyOnAny(
                    // Если позже появятся пространства имён v2+, v1 не должен от них зависеть.
                    ".Features.v2",
                    ".Features.v3")
                .GetResult();

            var failingTypes = v1Result.FailingTypeNames ?? Array.Empty<string>();

            v1Result.IsSuccessful.ShouldBeTrue(
                $"Функции v1 в сборке «{module.FullName}» не должны зависеть от более новых версий функций. " +
                $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
        }
    }
}
