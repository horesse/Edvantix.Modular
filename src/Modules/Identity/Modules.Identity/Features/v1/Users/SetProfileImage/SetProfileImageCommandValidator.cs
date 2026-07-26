using EDV.Modules.Identity.Contracts.v1.Users.SetProfileImage;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Users.SetProfileImage;

public sealed class SetProfileImageCommandValidator : AbstractValidator<SetProfileImageCommand>
{
    public SetProfileImageCommandValidator()
    {
        // Пустое/null допустимо (очищает изображение). При указании должно выглядеть как URL или относительный путь.
        RuleFor(x => x.ImageUrl)
            .MaximumLength(2048)
            .When(x => !string.IsNullOrWhiteSpace(x.ImageUrl));
    }
}
