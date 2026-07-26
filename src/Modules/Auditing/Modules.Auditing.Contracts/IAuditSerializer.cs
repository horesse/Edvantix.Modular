namespace EDV.Modules.Auditing.Contracts;

/// <summary>
/// Детерминированная JSON-сериализация payload'ов (camelCase, enum-as-string, стабильный вывод).
/// </summary>
public interface IAuditSerializer
{
    string SerializePayload(object payload);
}