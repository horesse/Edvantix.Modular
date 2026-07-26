using EDV.Modules.Identity.Contracts.v1.Users.RegisterUser;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Users.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Требуется имя.")
            .MaximumLength(100).WithMessage("Имя не должно превышать 100 символов.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Требуется фамилия.")
            .MaximumLength(100).WithMessage("Фамилия не должна превышать 100 символов.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Требуется email.")
            .EmailAddress().WithMessage("Требуется корректный адрес email.");

        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Требуется имя пользователя.")
            .MinimumLength(3).WithMessage("Имя пользователя должно содержать не менее 3 символов.")
            .MaximumLength(50).WithMessage("Имя пользователя не должно превышать 50 символов.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Требуется пароль.")
            .MinimumLength(6).WithMessage("Пароль должен содержать не менее 6 символов.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Требуется подтверждение пароля.")
            .Equal(x => x.Password).WithMessage("Пароли не совпадают.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("Номер телефона не должен превышать 20 символов.")
            .When(x => x.PhoneNumber is not null);
    }
}