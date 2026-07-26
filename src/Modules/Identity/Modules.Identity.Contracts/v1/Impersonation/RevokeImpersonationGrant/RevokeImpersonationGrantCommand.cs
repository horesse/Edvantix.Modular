using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Impersonation.RevokeImpersonationGrant;

public sealed record RevokeImpersonationGrantCommand(
    Guid GrantId,
    string? Reason)
    : ICommand<ImpersonationGrantDto>;
