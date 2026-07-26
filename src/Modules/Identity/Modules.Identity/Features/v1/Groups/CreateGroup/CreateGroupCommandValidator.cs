using EDV.Modules.Identity.Contracts.v1.Groups.CreateGroup;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Groups.CreateGroup;

public sealed class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Требуется имя группы.")
            .MaximumLength(256).WithMessage("Имя группы не должно превышать 256 символов.");

        RuleFor(x => x.Description)
            .MaximumLength(1024).WithMessage("Описание не должно превышать 1024 символа.");
    }
}