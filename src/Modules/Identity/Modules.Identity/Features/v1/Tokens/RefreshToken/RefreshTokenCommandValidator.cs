using EDV.Modules.Identity.Contracts.v1.Tokens.RefreshToken;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Tokens.RefreshToken;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        // Token намеренно не валидируется — см. RefreshTokenCommand о причине его
        // необязательности. Обработчик сверяет его только при наличии.
        RuleFor(p => p.RefreshToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty();
    }
}