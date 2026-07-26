using EDV.Modules.Identity.Contracts.v1.Impersonation.GetImpersonationGrants;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Impersonation.GetImpersonationGrants;

public sealed class GetImpersonationGrantsQueryValidator : AbstractValidator<GetImpersonationGrantsQuery>
{
    public const int MaxTake = 500;

    public GetImpersonationGrantsQueryValidator()
    {
        RuleFor(q => q.Take)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxTake)
            .WithMessage($"Take must be between 1 and {MaxTake}.");
    }
}
