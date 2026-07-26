namespace EDV.Framework.Shared.Persistence;

/// <summary>
/// Общий контракт для пагинации и сортировки, который может быть реализован
/// или расширен типами запросов, специфичными для модулей.
/// </summary>
public interface IPagedQuery
{
    /// <summary>
    /// Номер страницы (начиная с 1). Значения меньше 1 нормализуются до 1.
    /// </summary>
    int? PageNumber { get; set; }

    /// <summary>
    /// Запрашиваемый размер страницы. Реализации могут устанавливать ограничения.
    /// </summary>
    int? PageSize { get; set; }

    /// <summary>
    /// Выражение сортировки по нескольким колонкам, например: "Name,-CreatedOn".
    /// Префикс "-" указывает на сортировку по убыванию.
    /// </summary>
    string? Sort { get; set; }
}