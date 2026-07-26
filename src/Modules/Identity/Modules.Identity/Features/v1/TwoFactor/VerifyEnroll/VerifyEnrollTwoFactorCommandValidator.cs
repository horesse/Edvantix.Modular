using EDV.Modules.Identity.Contracts.v1.TwoFactor;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.TwoFactor.VerifyEnroll;

public sealed class VerifyEnrollTwoFactorCommandValidator : AbstractValidator<VerifyEnrollTwoFactorCommand>
{
    public VerifyEnrollTwoFactorCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MinimumLength(6)
            .MaximumLength(10); // допускаем пробелы; обработчик их убирает
    }
}
