using Mediator;
using Shouldly;
using System.Reflection;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Тесты для проверки того, что у каждого обработчика команды/запроса есть соответствующий валидатор.
/// Это обеспечивает покрытие валидацией во всех функциях.
/// </summary>
public class HandlerValidatorPairingTests
{
    private static readonly Assembly[] ModuleAssemblies = ModuleAssemblyDiscovery.GetModuleAssemblies();

    // Известные отсутствующие валидаторы (предстоит реализовать)
    private static readonly string[] KnownMissingCommandHandlers = [
        "EDV.Modules.Billing.Features.v1.Invoices.VoidInvoice.VoidInvoiceCommandHandler",
        "EDV.Modules.Billing.Features.v1.Invoices.MarkInvoicePaid.MarkInvoicePaidCommandHandler",
        "EDV.Modules.Billing.Features.v1.Invoices.IssueInvoice.IssueInvoiceCommandHandler",
        "EDV.Modules.Catalog.Features.v1.Products.RestoreProduct.RestoreProductCommandHandler",
        "EDV.Modules.Catalog.Features.v1.Products.DeleteProduct.DeleteProductCommandHandler",
        "EDV.Modules.Catalog.Features.v1.Categories.RestoreCategory.RestoreCategoryCommandHandler",
        "EDV.Modules.Catalog.Features.v1.Categories.DeleteCategory.DeleteCategoryCommandHandler",
        "EDV.Modules.Catalog.Features.v1.Brands.RestoreBrand.RestoreBrandCommandHandler",
        "EDV.Modules.Catalog.Features.v1.Brands.DeleteBrand.DeleteBrandCommandHandler",
        "EDV.Modules.Identity.Features.v1.TwoFactor.Enroll.EnrollTwoFactorCommandHandler",
        "EDV.Modules.Identity.Features.v1.Impersonation.EndImpersonation.EndImpersonationCommandHandler",
        "EDV.Modules.Multitenancy.Features.v1.TenantProvisioning.RetryTenantProvisioning.RetryTenantProvisioningCommandHandler",
        "EDV.Modules.Multitenancy.Features.v1.ResetTenantTheme.ResetTenantThemeCommandHandler",
        "EDV.Modules.Tickets.Features.v1.Tickets.RestoreTicket.RestoreTicketCommandHandler",
        "EDV.Modules.Tickets.Features.v1.Tickets.ResolveTicket.ResolveTicketCommandHandler",
        "EDV.Modules.Tickets.Features.v1.Tickets.ReopenTicket.ReopenTicketCommandHandler",
        "EDV.Modules.Tickets.Features.v1.Tickets.AssignTicket.AssignTicketCommandHandler"
    ];

    private static readonly string[] KnownMissingQueryHandlers = [
        "EDV.Modules.Billing.Features.v1.Invoices.GetMyInvoices.GetMyInvoicesQueryHandler",
        "EDV.Modules.Billing.Features.v1.Invoices.GetInvoices.GetInvoicesQueryHandler",
        "EDV.Modules.Catalog.Features.v1.Products.SearchProducts.SearchProductsQueryHandler",
        "EDV.Modules.Catalog.Features.v1.Products.ListTrashedProducts.ListTrashedProductsQueryHandler",
        "EDV.Modules.Catalog.Features.v1.Categories.SearchCategories.SearchCategoriesQueryHandler",
        "EDV.Modules.Catalog.Features.v1.Categories.ListTrashedCategories.ListTrashedCategoriesQueryHandler",
        "EDV.Modules.Catalog.Features.v1.Brands.SearchBrands.SearchBrandsQueryHandler",
        "EDV.Modules.Catalog.Features.v1.Brands.ListTrashedBrands.ListTrashedBrandsQueryHandler",
        "EDV.Modules.Identity.Features.v1.Sessions.GetTenantSessions.GetTenantSessionsQueryHandler",
        "EDV.Modules.Tickets.Features.v1.Tickets.SearchTickets.SearchTicketsQueryHandler",
        "EDV.Modules.Tickets.Features.v1.Tickets.ListTrashedTickets.ListTrashedTicketsQueryHandler"
    ];

    [Fact]
    public void CommandHandlers_Should_Have_Corresponding_Validators()
    {
        var missingValidators = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            // Находим все типы обработчиков команд
            var commandHandlerTypes = module.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    (i.GetGenericTypeDefinition() == typeof(ICommandHandler<>) ||
                     i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>))));

            foreach (var handlerType in commandHandlerTypes)
            {
                if (KnownMissingCommandHandlers.Contains(handlerType.FullName)) continue;
                // Извлекаем тип команды из интерфейса обработчика
                var handlerInterface = handlerType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType &&
                        (i.GetGenericTypeDefinition() == typeof(ICommandHandler<>) ||
                         i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)));

                if (handlerInterface == null) continue;

                var commandType = handlerInterface.GetGenericArguments()[0];
                var commandName = commandType.Name;

                // Ищем валидатор в том же пространстве имён или рядом
                var expectedValidatorName = commandName + "Validator";

                // Проверяем наличие валидатора в той же сборке
                var validatorExists = module.GetTypes()
                    .Any(t => t.Name == expectedValidatorName ||
                              t.Name == commandName.Replace("Command", "", StringComparison.Ordinal) + "CommandValidator" ||
                              t.Name == commandName.Replace("Command", "", StringComparison.Ordinal) + "Validator");

                if (!validatorExists)
                {
                    // Проверяем, есть ли у самого типа команды атрибуты валидации (допустимая альтернатива)
                    var hasValidationAttributes = commandType
                        .GetProperties()
                        .Any(p => p.GetCustomAttributes()
                            .Any(a => a.GetType().Name.Contains("Required", StringComparison.Ordinal) ||
                                     a.GetType().Name.Contains("Range", StringComparison.Ordinal) ||
                                     a.GetType().Name.Contains("StringLength", StringComparison.Ordinal)));

                    if (!hasValidationAttributes)
                    {
                        missingValidators.Add($"{handlerType.FullName} -> отсутствует {expectedValidatorName}");
                    }
                }
            }
        }

        missingValidators.ShouldBeEmpty(
            $"Найдено {missingValidators.Count} обработчик(ов) команд без валидаторов. " +
            $"У каждого обработчика команды должен быть соответствующий валидатор FluentValidation. " +
            $"Отсутствуют: {string.Join(", ", missingValidators)}");
    }

    [Fact]
    public void QueryHandlers_With_Pagination_Should_Have_Validators()
    {
        var missingValidators = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            // Находим все типы обработчиков запросов
            var queryHandlerTypes = module.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)));

            foreach (var handlerType in queryHandlerTypes)
            {
                if (KnownMissingQueryHandlers.Contains(handlerType.FullName)) continue;
                // Извлекаем тип запроса из интерфейса обработчика
                var handlerInterface = handlerType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType &&
                        i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));

                if (handlerInterface == null) continue;

                var queryType = handlerInterface.GetGenericArguments()[0];

                // Проверяем, есть ли у этого запроса свойства пагинации (PageNumber, PageSize и т.д.)
                var hasPagination = queryType.GetProperties()
                    .Any(p => p.Name.Equals("PageNumber", StringComparison.OrdinalIgnoreCase) ||
                              p.Name.Equals("PageSize", StringComparison.OrdinalIgnoreCase) ||
                              p.Name.Equals("Skip", StringComparison.OrdinalIgnoreCase) ||
                              p.Name.Equals("Take", StringComparison.OrdinalIgnoreCase));

                if (hasPagination)
                {
                    var queryName = queryType.Name;
                    var expectedValidatorName = queryName + "Validator";

                    // Проверяем наличие валидатора в той же сборке
                    var validatorExists = module.GetTypes()
                        .Any(t => t.Name == expectedValidatorName ||
                                  t.Name == queryName.Replace("Query", "", StringComparison.Ordinal) + "QueryValidator" ||
                                  t.Name == queryName.Replace("Query", "", StringComparison.Ordinal) + "Validator");

                    if (!validatorExists)
                    {
                        missingValidators.Add(
                            $"{handlerType.FullName} обрабатывает постраничный запрос, но не имеет валидатора");
                    }
                }
            }
        }

        missingValidators.ShouldBeEmpty(
            $"У постраничных запросов должны быть валидаторы для проверки границ PageNumber/PageSize. " +
            $"Отсутствуют: {string.Join(", ", missingValidators)}");
    }

    [Fact]
    public void Validators_Should_Match_Command_Or_Query_Types()
    {
        var orphanedValidators = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            // Находим все валидаторы (классы, унаследованные от AbstractValidator<T>)
            var validatorTypes = module.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.BaseType != null &&
                           t.BaseType.IsGenericType &&
                           t.BaseType.GetGenericTypeDefinition().Name.Contains("AbstractValidator", StringComparison.Ordinal));

            foreach (var validatorType in validatorTypes)
            {
                // Пропускаем вложенные валидаторы (например, LayoutValidator внутри UpdateTenantThemeCommandValidator)
                if (validatorType.IsNested) continue;

                // Получаем валидируемый тип
                var validatedType = validatorType.BaseType?.GetGenericArguments().FirstOrDefault();
                if (validatedType == null) continue;

                // Проверяем, является ли валидируемый тип командой или запросом
                bool isCommand = validatedType.Name.EndsWith("Command", StringComparison.Ordinal);
                bool isQuery = validatedType.Name.EndsWith("Query", StringComparison.Ordinal);

                if (!isCommand && !isQuery)
                {
                    // Допускаем валидаторы для других типов (например, DTO), но отмечаем их
                    continue;
                }

                // Проверяем, соответствует ли имя валидатора соглашению
                var expectedName = validatedType.Name + "Validator";
                if (!validatorType.Name.Equals(expectedName, StringComparison.Ordinal))
                {
                    // Допускаем некоторую гибкость в именовании
                    var altName = validatedType.Name.Replace("Command", "", StringComparison.Ordinal).Replace("Query", "", StringComparison.Ordinal) +
                                  (isCommand ? "CommandValidator" : "QueryValidator");
                    var altName2 = validatedType.Name.Replace("Command", "", StringComparison.Ordinal).Replace("Query", "", StringComparison.Ordinal) + "Validator";
                    if (!validatorType.Name.Equals(altName, StringComparison.Ordinal) && !validatorType.Name.Equals(altName2, StringComparison.Ordinal))
                    {
                        orphanedValidators.Add(
                            $"{validatorType.FullName} валидирует {validatedType.Name}, но имя не соответствует соглашению");
                    }
                }
            }
        }

        orphanedValidators.ShouldBeEmpty(
            $"Найдено {orphanedValidators.Count} валидатор(ов) с неверным именованием. " +
            $"Валидаторы должны называться {{CommandName}}Validator или {{CommandName}}CommandValidator. " +
            $"Нарушения: {string.Join(", ", orphanedValidators)}");
    }
}
