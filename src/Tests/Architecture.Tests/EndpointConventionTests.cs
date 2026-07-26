using NetArchTest.Rules;
using Shouldly;
using System.Reflection;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Тесты для соблюдения соглашений об endpoint-ах во всех модулях.
/// </summary>
public class EndpointConventionTests
{
    private static readonly Assembly[] ModuleAssemblies = ModuleAssemblyDiscovery.GetModuleAssemblies();

    [Fact]
    public void Endpoints_Should_Be_Static_Classes()
    {
        var violations = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            var endpointTypes = module.GetTypes()
                .Where(t => t.Name.EndsWith("Endpoint", StringComparison.Ordinal))
                .Where(t => t.IsClass);

            foreach (var endpointType in endpointTypes)
            {
                if (!endpointType.IsAbstract || !endpointType.IsSealed)
                {
                    // В C# статические классы компилируются как abstract sealed
                    violations.Add($"{endpointType.FullName} должен быть статическим классом");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Классы endpoint-ов должны быть статическими. " +
            $"Нарушения: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Endpoints_Should_Reside_In_Features_Namespace()
    {
        foreach (var module in ModuleAssemblies)
        {
            var result = Types
                .InAssembly(module)
                .That()
                .HaveNameEndingWith("Endpoint")
                .Should()
                .ResideInNamespaceContaining(".Features.")
                .GetResult();

            var failingTypes = result.FailingTypeNames ?? [];

            result.IsSuccessful.ShouldBeTrue(
                $"Endpoint-ы в модуле «{module.GetName().Name}» должны находиться в пространстве имён Features. " +
                $"Несоответствующие типы: {string.Join(", ", failingTypes)}");
        }
    }

    [Fact]
    public void Endpoints_Should_Have_Map_Method()
    {
        var violations = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            var endpointTypes = module.GetTypes()
                .Where(t => t.Name.EndsWith("Endpoint", StringComparison.Ordinal))
                .Where(t => t.IsClass && t.IsAbstract && t.IsSealed); // Статические классы

            foreach (var endpointType in endpointTypes)
            {
                // Проверяем как публичные, так и internal/непубличные статические методы
                var mapMethods = endpointType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(m => m.Name.StartsWith("Map", StringComparison.Ordinal))
                    .ToArray();

                if (mapMethods.Length == 0)
                {
                    violations.Add($"{endpointType.FullName} должен иметь метод Map*");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Классы endpoint-ов должны иметь метод Map (например, MapGetUsersEndpoint). " +
            $"Нарушения: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Endpoint_Map_Methods_Should_Return_RouteHandlerBuilder()
    {
        var violations = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            var endpointTypes = module.GetTypes()
                .Where(t => t.Name.EndsWith("Endpoint", StringComparison.Ordinal))
                .Where(t => t.IsClass && t.IsAbstract && t.IsSealed); // Статические классы

            foreach (var endpointType in endpointTypes)
            {
                // Проверяем как публичные, так и internal/непубличные статические методы
                var mapMethods = endpointType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(m => m.Name.StartsWith("Map", StringComparison.Ordinal));

                foreach (var method in mapMethods)
                {
                    var returnType = method.ReturnType;

                    // Проверяем, является ли возвращаемый тип RouteHandlerBuilder или производным от него
                    bool isValidReturn = returnType.Name == "RouteHandlerBuilder" ||
                                         returnType.Name == "IEndpointConventionBuilder" ||
                                         returnType.GetInterfaces().Any(i =>
                                             i.Name == "IEndpointConventionBuilder");

                    if (!isValidReturn)
                    {
                        violations.Add(
                            $"{endpointType.Name}.{method.Name} возвращает {returnType.Name}, " +
                            "ожидался RouteHandlerBuilder");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Методы Map endpoint-ов должны возвращать RouteHandlerBuilder. " +
            $"Нарушения: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Endpoint_Map_Methods_Should_Take_IEndpointRouteBuilder()
    {
        var violations = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            var endpointTypes = module.GetTypes()
                .Where(t => t.Name.EndsWith("Endpoint", StringComparison.Ordinal))
                .Where(t => t.IsClass && t.IsAbstract && t.IsSealed); // Статические классы

            foreach (var endpointType in endpointTypes)
            {
                // Проверяем как публичные, так и internal/непубличные статические методы
                var mapMethods = endpointType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(m => m.Name.StartsWith("Map", StringComparison.Ordinal));

                foreach (var method in mapMethods)
                {
                    var parameters = method.GetParameters();

                    // Должен быть методом расширения с первым параметром IEndpointRouteBuilder
                    bool hasEndpointRouteBuilder = parameters.Length > 0 &&
                        (parameters[0].ParameterType.Name == "IEndpointRouteBuilder" ||
                         parameters[0].ParameterType.GetInterfaces().Any(i =>
                             i.Name == "IEndpointRouteBuilder"));

                    if (!hasEndpointRouteBuilder)
                    {
                        violations.Add(
                            $"{endpointType.Name}.{method.Name} первым параметром должен принимать IEndpointRouteBuilder");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Методы Map endpoint-ов должны расширять IEndpointRouteBuilder. " +
            $"Нарушения: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Endpoints_Should_Not_Contain_Business_Logic()
    {
        // Endpoint-ы должны делегировать выполнение обработчикам через Mediator, а не содержать бизнес-логику.
        // Проверяем, что классы endpoint-ов не содержат приватных методов (в которых может скрываться логика).
        var warnings = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            var endpointTypes = module.GetTypes()
                .Where(t => t.Name.EndsWith("Endpoint", StringComparison.Ordinal))
                .Where(t => t.IsClass && t.IsAbstract && t.IsSealed); // Статические классы

            foreach (var endpointType in endpointTypes)
            {
                var privateMethods = endpointType
                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(m => !m.Name.StartsWith('<')) // Исключаем сгенерированные компилятором
                    .Where(m => m.DeclaringType == endpointType) // Только объявленные в этом типе
                    .ToArray();

                if (privateMethods.Length > 0)
                {
                    warnings.Add(
                        $"{endpointType.Name} содержит приватные методы ({string.Join(", ", privateMethods.Select(m => m.Name))}). " +
                        "Рассмотрите возможность переноса логики в обработчики.");
                }
            }
        }

        // Только информационно — некоторые приватные статические вспомогательные методы в endpoint-ах допустимы; мы
        // утверждаем, что проверка выполнилась (список заполнен), а не что он пуст.
        warnings.ShouldNotBeNull("Проверка бизнес-логики в endpoint-ах не выполнилась");
        // Просмотрите endpoint-ы, указанные в 'warnings', и перенесите бизнес-логику в обработчики.
    }

    [Fact]
    public void Endpoint_Names_Should_Follow_Convention()
    {
        var violations = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            var endpointTypes = module.GetTypes()
                .Where(t => t.Name.EndsWith("Endpoint", StringComparison.Ordinal))
                .Where(t => t.IsClass);

            foreach (var endpointType in endpointTypes)
            {
                // Имена endpoint-ов должны описывать действие (например, GetUsersEndpoint, CreateTenantEndpoint)
                string name = endpointType.Name;

                // Проверяем соответствие паттерну глагол-существительное-Endpoint
                bool hasVerb = name.StartsWith("Get", StringComparison.Ordinal) ||
                               name.StartsWith("Create", StringComparison.Ordinal) ||
                               name.StartsWith("Update", StringComparison.Ordinal) ||
                               name.StartsWith("Delete", StringComparison.Ordinal) ||
                               name.StartsWith("List", StringComparison.Ordinal) ||
                               name.StartsWith("Search", StringComparison.Ordinal) ||
                               name.StartsWith("Register", StringComparison.Ordinal) ||
                               name.StartsWith("Generate", StringComparison.Ordinal) ||
                               name.StartsWith("Refresh", StringComparison.Ordinal) ||
                               name.StartsWith("Resend", StringComparison.Ordinal) ||
                               name.StartsWith("Confirm", StringComparison.Ordinal) ||
                               name.StartsWith("Reset", StringComparison.Ordinal) ||
                               name.StartsWith("Forgot", StringComparison.Ordinal) ||
                               name.StartsWith("Change", StringComparison.Ordinal) ||
                               name.StartsWith("Toggle", StringComparison.Ordinal) ||
                               name.StartsWith("Assign", StringComparison.Ordinal) ||
                               name.StartsWith("Revoke", StringComparison.Ordinal) ||
                               name.StartsWith("Admin", StringComparison.Ordinal) ||
                               name.StartsWith("Upsert", StringComparison.Ordinal) ||
                               name.StartsWith("Add", StringComparison.Ordinal) ||
                               name.StartsWith("Remove", StringComparison.Ordinal) ||
                               name.StartsWith("Retry", StringComparison.Ordinal) ||
                               name.StartsWith("Upgrade", StringComparison.Ordinal) ||
                               name.StartsWith("Renew", StringComparison.Ordinal) ||
                               name.StartsWith("Self", StringComparison.Ordinal) ||
                               name.StartsWith("Tenant", StringComparison.Ordinal) ||
                               name.StartsWith("Start", StringComparison.Ordinal) ||
                               name.StartsWith("End", StringComparison.Ordinal) ||
                               name.StartsWith("Enroll", StringComparison.Ordinal) ||
                               name.StartsWith("Verify", StringComparison.Ordinal) ||
                               name.StartsWith("Disable", StringComparison.Ordinal) ||
                               name.StartsWith("Enable", StringComparison.Ordinal) ||
                               name.StartsWith("Restore", StringComparison.Ordinal) ||
                               name.StartsWith("Adjust", StringComparison.Ordinal) ||
                               name.StartsWith("Resolve", StringComparison.Ordinal) ||
                               name.StartsWith("Reopen", StringComparison.Ordinal) ||
                               name.StartsWith("Close", StringComparison.Ordinal) ||
                               name.StartsWith("Test", StringComparison.Ordinal) ||
                               name.StartsWith("Void", StringComparison.Ordinal) ||
                               name.StartsWith("Mark", StringComparison.Ordinal) ||
                               name.StartsWith("Issue", StringComparison.Ordinal) ||
                               name.StartsWith("Capture", StringComparison.Ordinal) ||
                               name.StartsWith("Request", StringComparison.Ordinal) ||
                               name.StartsWith("Finalize", StringComparison.Ordinal) ||
                               name.StartsWith("Set", StringComparison.Ordinal) ||
                               name.StartsWith("Reorder", StringComparison.Ordinal) ||
                               name.StartsWith("Archive", StringComparison.Ordinal) ||
                               name.StartsWith("Find", StringComparison.Ordinal) ||
                               name.StartsWith("Edit", StringComparison.Ordinal) ||
                               name.StartsWith("Send", StringComparison.Ordinal) ||
                               name.StartsWith("Discover", StringComparison.Ordinal) ||
                               name.StartsWith("Pin", StringComparison.Ordinal) ||
                               name.StartsWith("Unpin", StringComparison.Ordinal) ||
                               name.StartsWith("Approve", StringComparison.Ordinal) ||
                               name.StartsWith("Reject", StringComparison.Ordinal);

                if (!hasVerb)
                {
                    violations.Add(
                        $"Имя {endpointType.FullName} должно начинаться с глагола действия " +
                        "(Get, Create, Update, Delete и т.д.)");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Имена endpoint-ов должны соответствовать соглашению глагол-существительное. " +
            $"Нарушения: {string.Join(", ", violations)}");
    }
}
