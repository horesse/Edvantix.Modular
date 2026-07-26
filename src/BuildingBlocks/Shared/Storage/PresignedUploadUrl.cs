namespace EDV.Framework.Shared.Storage;

/// <summary>
/// Кратковременный предварительно подписанный URL, который браузер может использовать для прямой отправки (PUT) байтов
/// в S3-совместимое хранилище, вместе с заголовками, которые подпись требует включить в запрос без изменений.
/// </summary>
public sealed record PresignedUploadUrl(
    Uri Url,
    IReadOnlyDictionary<string, string> RequiredHeaders,
    DateTimeOffset ExpiresAt);