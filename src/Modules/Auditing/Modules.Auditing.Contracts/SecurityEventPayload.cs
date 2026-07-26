namespace EDV.Modules.Auditing.Contracts;

public sealed record SecurityEventPayload(
    SecurityAction Action,
    string? SubjectId,
    string? ClientId,
    string? AuthMethod,   // Password, OIDC и т.д.
    string? ReasonCode,   // InvalidPassword, LockedOut и т.д.
    IReadOnlyDictionary<string, object?>? ClaimsSnapshot
);
