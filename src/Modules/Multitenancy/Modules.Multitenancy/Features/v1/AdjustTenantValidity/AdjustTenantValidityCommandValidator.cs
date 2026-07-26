using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Multitenancy.Contracts.v1.AdjustTenantValidity;
using FluentValidation;

namespace EDV.Modules.Multitenancy.Features.v1.AdjustTenantValidity;

public sealed class AdjustTenantValidityCommandValidator : AbstractValidator<AdjustTenantValidityCommand>
{
    public AdjustTenantValidityCommandValidator()
    {
        RuleFor(t => t.TenantId).NotEmpty();

        // Срок действия корневого операторского тенанта никогда не должен истекать — запрещаем изменение
        // его срока действия (по аналогии с проверками Activate/Deactivate, которые уже отклоняют корневой тенант).
        RuleFor(t => t.TenantId)
            .Must(id => !string.Equals(id, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
            .WithMessage("Нельзя изменить срок действия корневого операторского тенанта.");

        RuleFor(t => t.ValidUpto)
            .Must(d => d != default)
            .WithMessage("Необходимо указать корректную дату 'validUpto'.");
    }
}
