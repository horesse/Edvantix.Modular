namespace EDV.Framework.Core.Domain;

/// <summary>
/// Помечает сущность как поддерживающую мягкое удаление.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Возвращает значение, указывающее, удалена ли сущность.
    /// </summary>
    bool IsDeleted { get; }

    /// <summary>
    /// Возвращает временную метку UTC, когда сущность была удалена.
    /// </summary>
    DateTimeOffset? DeletedOnUtc { get; }

    /// <summary>
    /// Возвращает идентификатор пользователя, удалившего сущность.
    /// </summary>
    string? DeletedBy { get; }
}