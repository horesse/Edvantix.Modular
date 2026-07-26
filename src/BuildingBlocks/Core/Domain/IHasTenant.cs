namespace EDV.Framework.Core.Domain;

/// <summary>
/// Связывает сущность с арендатором.
/// </summary>
public interface IHasTenant
{
    /// <summary>
    /// Возвращает идентификатор арендатора.
    /// </summary>
    string TenantId { get; }
}