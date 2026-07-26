using EDV.Modules.Identity.Contracts.v1.Groups.UpdateGroup;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Groups.UpdateGroup;

public sealed class UpdateGroupCommandValidator : AbstractValidator<UpdateGroupCommand>
{
    public UpdateGroupCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Требуется ID группы.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Требуется имя группы.")
            .MaximumLength(256).WithMessage("Имя группы не должно превышать 256 символов.");

        RuleFor(x => x.Description)
            .MaximumLength(1024).WithMessage("Описание не должно превышать 1024 символа.");
    }
}