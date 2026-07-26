namespace EDV.Framework.Core.Domain;

/// <summary>
/// Определяет метаданные аудита для сущности.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// Возвращает временную метку UTC, когда сущность была создана.
    /// </summary>
    DateTimeOffset CreatedOnUtc { get; }

    /// <summary>
    /// Возвращает идентификатор создателя.
    /// </summary>
    string? CreatedBy { get; }

    /// <summary>
    /// Возвращает временную метку UTC, когда сущность была последний раз изменена.
    /// </summary>
    DateTimeOffset? LastModifiedOnUtc { get; }

    /// <summary>
    /// Возвращает идентификатор последнего редактора.
    /// </summary>
    string? LastModifiedBy { get; }
}