using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Impersonation.StartImpersonation;

// DurationMinutes: запрошенное время жизни токена, ограничиваемое на сервере
// StartImpersonationCommandValidator.MaxImpersonationMinutes (60); null → JwtOptions.AccessTokenMinutes.
public sealed record StartImpersonationCommand(
    string TargetUserId,
    string TargetTenantId,
    string? Reason,
    int? DurationMinutes = null)
    : ICommand<ImpersonationResponse>;