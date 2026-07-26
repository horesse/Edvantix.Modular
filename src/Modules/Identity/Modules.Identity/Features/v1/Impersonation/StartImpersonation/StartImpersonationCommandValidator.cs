using EDV.Modules.Identity.Contracts.v1.Impersonation.StartImpersonation;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Impersonation.StartImpersonation;

public sealed class StartImpersonationCommandValidator : AbstractValidator<StartImpersonationCommand>
{
    /// <summary>
    /// Верхняя граница срока жизни токена имперсонализации — сервер молча ограничит
    /// её этим значением, даже если валидатор пропустит, но мы отклоняем очевидные
    /// злоупотребления (отрицательные, нулевые или абсурдные значения) заранее.
    /// </summary>
    public const int MaxImpersonationMinutes = 60;

    public StartImpersonationCommandValidator()
    {
        RuleFor(p => p.TargetUserId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty();

        RuleFor(p => p.TargetTenantId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty();

        RuleFor(p => p.DurationMinutes!.Value)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxImpersonationMinutes)
            .WithMessage($"Длительность должна быть от 1 до {MaxImpersonationMinutes} минут.")
            .When(p => p.DurationMinutes.HasValue);
    }
}
