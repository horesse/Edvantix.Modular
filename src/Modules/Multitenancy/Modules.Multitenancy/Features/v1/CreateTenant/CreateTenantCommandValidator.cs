using EDV.Framework.Persistence;
using EDV.Modules.Multitenancy.Contracts;
using EDV.Modules.Multitenancy.Contracts.v1.CreateTenant;
using FluentValidation;

namespace EDV.Modules.Multitenancy.Features.v1.CreateTenant;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator(ITenantService tenantService, IConnectionStringValidator connectionStringValidator)
    {
        RuleFor(t => t.Id).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustAsync(async (id, ct) => !await tenantService.ExistsWithIdAsync(id, ct).ConfigureAwait(false))
            .WithMessage((_, id) => $"Тенант {id} уже существует.");

        RuleFor(t => t.Name).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustAsync(async (name, ct) => !await tenantService.ExistsWithNameAsync(name!, ct).ConfigureAwait(false))
            .WithMessage((_, name) => $"Тенант {name} уже существует.");

        RuleFor(t => t.ConnectionString).Cascade(CascadeMode.Stop)
            .Must((_, cs) => string.IsNullOrWhiteSpace(cs) || connectionStringValidator.TryValidate(cs))
            .WithMessage("Некорректная строка подключения.");

        RuleFor(t => t.AdminEmail).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .EmailAddress();

        // Пароль администратора задаётся оператором. Минимум в 8 символов соответствует политике Identity;
        // требования к составу символов (цифра/верхний регистр/не буква) применяются позже валидаторами
        // паролей Identity на этапе заполнения данными.
        RuleFor(t => t.AdminPassword).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MinimumLength(8)
            .WithMessage("Пароль администратора должен содержать не менее 8 символов.");

        // Необязательное поле — при null/пустом значении используется настроенный план по умолчанию. Если
        // значение указано, оно должно быть slug'ом плана в нижнем регистре; существование проверяется
        // вызовом GetPlanTerm в обработчике.
        RuleFor(t => t.PlanKey)
            .Matches("^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$")
            .When(t => !string.IsNullOrWhiteSpace(t.PlanKey))
            .WithMessage("Ключ плана должен быть slug'ом в нижнем регистре (a-z, 0-9, дефис).");
    }
}