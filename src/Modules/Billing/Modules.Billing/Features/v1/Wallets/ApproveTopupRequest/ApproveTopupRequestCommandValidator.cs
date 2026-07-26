using EDV.Modules.Billing.Contracts.v1.Wallets;
using FluentValidation;

namespace EDV.Modules.Billing.Features.v1.Wallets.ApproveTopupRequest;

public sealed class ApproveTopupRequestCommandValidator : AbstractValidator<ApproveTopupRequestCommand>
{
    public ApproveTopupRequestCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
