namespace EDV.Framework.Shared.Storage;

/// <summary>
/// Метаданные, возвращаемые при выполнении HEAD-запроса к объекту в хранилище. Используются модулем Files
/// при финализации для проверки размера и типа содержимого по сравнению со значениями,
/// указанными при получении URL для загрузки.
/// </summary>
public sealed record StoredObjectMetadata(
    long SizeBytes,
    string ContentType,
    DateTimeOffset LastModified,
    string? ETag);