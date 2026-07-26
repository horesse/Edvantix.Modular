using EDV.Framework.Storage;
using EDV.Modules.Identity.Contracts.v1.Users.UpdateUser;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Users.UpdateUser;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Требуется ID пользователя.");

        RuleFor(x => x.FirstName)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.FirstName));

        RuleFor(x => x.LastName)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.LastName));

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(15)
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        When(x => x.Image is not null, () =>
        {
            RuleFor(x => x.Image!)
                .SetValidator(new UserImageValidator(FileType.Image));
        });

        // Не допускаем одновременное удаление и загрузку изображения
        RuleFor(x => x)
            .Must(x => !(x.DeleteCurrentImage && x.Image is not null))
            .WithMessage("Нельзя одновременно загрузить новое изображение и удалить текущее.");
    }
}