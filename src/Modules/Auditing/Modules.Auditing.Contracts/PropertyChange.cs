namespace EDV.Modules.Auditing.Contracts;

/// <summary>
/// Одно изменение свойства для аудита изменения сущности.
/// </summary>
public sealed record PropertyChange(
    string Name,
    string? DataType,   // например, "string", "int", "datetime"
    object? OldValue,
    object? NewValue,
    bool IsSensitive    // true => значение уже замаскировано/захешировано
);
