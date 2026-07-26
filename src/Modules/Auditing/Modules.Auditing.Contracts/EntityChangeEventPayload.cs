namespace EDV.Modules.Auditing.Contracts;

public sealed record EntityChangeEventPayload(
    string DbContext,
    string? Schema,
    string Table,
    string EntityName,
    string Key,                          // унифицированный строковый ключ (например, "Id:42" или "TenantId:1|UserId:42")
    EntityOperation Operation,
    IReadOnlyList<PropertyChange> Changes,
    string? TransactionId
);
