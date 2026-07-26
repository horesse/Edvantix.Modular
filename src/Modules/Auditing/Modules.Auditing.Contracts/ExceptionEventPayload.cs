namespace EDV.Modules.Auditing.Contracts;

public sealed record ExceptionEventPayload(
    ExceptionArea Area,
    string ExceptionType,
    string Message,
    IReadOnlyList<string> StackTop,                     // ограниченное число фреймов
    IReadOnlyDictionary<string, object?>? Data,
    string? RouteOrLocation
);
