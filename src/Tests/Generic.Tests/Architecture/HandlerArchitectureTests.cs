using Mediator;
using System.Reflection;
using System.Text;

namespace Generic.Tests.Architecture;

/// <summary>
/// Архитектурные тесты для проверки того, что все обработчики следуют единым паттернам
/// во всех модулях (проверки на null, соглашения об именовании и т.д.).
/// </summary>
public sealed class HandlerArchitectureTests
{
    private static readonly Assembly[] ModuleAssemblies = ModuleAssemblyDiscovery.GetModuleAssemblies();

    [Fact]
    public void QueryHandlers_Should_FollowNamingConvention()
    {
        // Подготовка
        var failures = new List<string>();

        foreach (var assembly in ModuleAssemblies)
        {
            var handlerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)));

            foreach (var handlerType in handlerTypes)
            {
                if (!handlerType.Name.EndsWith("QueryHandler", StringComparison.Ordinal))
                {
                    failures.Add($"{handlerType.FullName} должен заканчиваться на 'QueryHandler'");
                }
            }
        }

        // Проверка
        failures.ShouldBeEmpty(BuildFailureMessage(failures));
    }

    [Fact]
    public void CommandHandlers_Should_FollowNamingConvention()
    {
        // Подготовка
        var failures = new List<string>();

        foreach (var assembly in ModuleAssemblies)
        {
            var handlerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    (i.GetGenericTypeDefinition() == typeof(ICommandHandler<>) ||
                     i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>))));

            foreach (var handlerType in handlerTypes)
            {
                if (!handlerType.Name.EndsWith("CommandHandler", StringComparison.Ordinal))
                {
                    failures.Add($"{handlerType.FullName} должен заканчиваться на 'CommandHandler'");
                }
            }
        }

        // Проверка
        failures.ShouldBeEmpty(BuildFailureMessage(failures));
    }

    [Fact]
    public void Handlers_Should_BeSealed()
    {
        // Подготовка
        var failures = new List<string>();

        foreach (var assembly in ModuleAssemblies)
        {
            var handlerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    (i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>) ||
                     i.GetGenericTypeDefinition() == typeof(ICommandHandler<>) ||
                     i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>))));

            foreach (var handlerType in handlerTypes)
            {
                if (!handlerType.IsSealed)
                {
                    failures.Add($"{handlerType.FullName} должен быть sealed");
                }
            }
        }

        // Проверка
        failures.ShouldBeEmpty(BuildFailureMessage(failures));
    }

    [Fact]
    public void Handlers_Should_HaveHandleMethod_WithCancellationToken()
    {
        // Подготовка
        var failures = new List<string>();

        foreach (var assembly in ModuleAssemblies)
        {
            var handlerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    (i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>) ||
                     i.GetGenericTypeDefinition() == typeof(ICommandHandler<>) ||
                     i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>))));

            foreach (var handlerType in handlerTypes)
            {
                var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "Handle");

                foreach (var method in handleMethods)
                {
                    var parameters = method.GetParameters();
                    var hasCancellationToken = parameters.Any(p => p.ParameterType == typeof(CancellationToken));

                    if (!hasCancellationToken)
                    {
                        failures.Add($"{handlerType.FullName}.Handle() должен иметь параметр CancellationToken");
                    }
                }
            }
        }

        // Проверка
        failures.ShouldBeEmpty(BuildFailureMessage(failures));
    }

    [Fact]
    public void Validators_Should_FollowNamingConvention()
    {
        // Подготовка
        var failures = new List<string>();

        foreach (var assembly in ModuleAssemblies)
        {
            var validatorTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.BaseType != null &&
                           t.BaseType.IsGenericType &&
                           t.BaseType.GetGenericTypeDefinition().Name.Contains("AbstractValidator", StringComparison.Ordinal));

            foreach (var validatorType in validatorTypes)
            {
                if (!validatorType.Name.EndsWith("Validator", StringComparison.Ordinal))
                {
                    failures.Add($"{validatorType.FullName} должен заканчиваться на 'Validator'");
                }
            }
        }

        // Проверка
        failures.ShouldBeEmpty(BuildFailureMessage(failures));
    }

    [Fact]
    public void Validators_Should_BeSealed()
    {
        // Подготовка
        var failures = new List<string>();

        foreach (var assembly in ModuleAssemblies)
        {
            var validatorTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.BaseType != null &&
                           t.BaseType.IsGenericType &&
                           t.BaseType.GetGenericTypeDefinition().Name.Contains("AbstractValidator", StringComparison.Ordinal));

            foreach (var validatorType in validatorTypes)
            {
                // Пропускаем partial-классы (например, UpdateTenantThemeCommandValidator использует partial
                // для сгенерированного исходным генератором регулярного выражения). Partial-классы не могут
                // быть sealed, но их вложенные валидаторы sealed.
                if (IsPartialClass(validatorType))
                {
                    continue;
                }

                if (!validatorType.IsSealed)
                {
                    failures.Add($"{validatorType.FullName} должен быть sealed");
                }
            }
        }

        // Проверка
        failures.ShouldBeEmpty(BuildFailureMessage(failures));
    }

    private static bool IsPartialClass(Type type)
    {
        // Partial-классы от генераторов исходного кода (например, GeneratedRegex) создают члены,
        // сгенерированные компилятором; обнаруживаем их по атрибуту GeneratedRegexAttribute на любом методе.
        return type.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Any(m => m.GetCustomAttributes()
                .Any(a => a.GetType().Name == "GeneratedRegexAttribute"));
    }

    private static string BuildFailureMessage(List<string> failures)
    {
        if (failures.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("Найдено ").Append(failures.Count).AppendLine(" нарушение(й):");
        foreach (var failure in failures)
        {
            sb.Append("  - ").AppendLine(failure);
        }
        return sb.ToString();
    }
}
