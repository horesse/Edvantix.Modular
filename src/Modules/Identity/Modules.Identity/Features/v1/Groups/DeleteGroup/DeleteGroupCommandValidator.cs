using EDV.Modules.Identity.Contracts.v1.Groups.DeleteGroup;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Groups.DeleteGroup;

public sealed class DeleteGroupCommandValidator : AbstractValidator<DeleteGroupCommand>
{
    public DeleteGroupCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Требуется ID группы.");
    }
}