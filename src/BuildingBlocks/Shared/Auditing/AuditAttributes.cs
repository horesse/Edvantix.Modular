namespace EDV.Framework.Shared.Auditing;

/// <summary>Помечает свойство, которое следует исключить из diff-сравнений и полезных нагрузок аудита.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class AuditIgnoreAttribute : Attribute { }

/// <summary>
/// Помечает свойство как конфиденциальное (подлежит маскировке или хешированию при сериализации).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class AuditSensitiveAttribute : Attribute
{
    public bool Hash { get; init; }
    public bool Redact { get; init; }

    public AuditSensitiveAttribute(bool hash = false, bool redact = false)
        => (Hash, Redact) = (hash, redact);
}