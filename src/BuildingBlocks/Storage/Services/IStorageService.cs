using EDV.Framework.Shared.Storage;
using EDV.Framework.Storage.DTOs;

namespace EDV.Framework.Storage.Services;

public interface IStorageService
{
    Task<string> UploadAsync<T>(
        FileUploadRequest request,
        FileType fileType,
        CancellationToken cancellationToken = default) where T : class;

    Task<FileDownloadResponse?> DownloadAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает размер в байтах объекта по пути <paramref name="path"/>, либо 0, если он не существует.
    /// Используется учётом квот для списания использования хранилища при удалении без необходимости
    /// отслеживать размеры на стороне вызывающего кода.
    /// </summary>
    Task<long> GetSizeAsync(string path, CancellationToken cancellationToken = default);

    Task RemoveAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Выпускает кратковременный предварительно подписанный PUT-URL, который браузер использует
    /// для прямой отправки байтов в S3-совместимое хранилище. Возвращает URL и любые заголовки,
    /// которые браузер ОБЯЗАН включить без изменений в свой PUT-запрос (обычно Content-Type,
    /// когда подпись это требует). Используется эндпоинтом <c>RequestUploadUrl</c> модуля Files.
    /// </summary>
    Task<PresignedUploadUrl> GenerateUploadUrlAsync(
        string storageKey,
        string contentType,
        long maxBytes,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Выпускает кратковременный предварительно подписанный GET-URL. Когда указан
    /// <paramref name="responseContentDisposition"/>, S3 отражает его в ответе на загрузку,
    /// чтобы браузер показал исходное имя файла, а не ключ хранилища.
    /// </summary>
    Task<Uri> GenerateDownloadUrlAsync(
        string storageKey,
        TimeSpan ttl,
        string? responseContentDisposition = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет HEAD-запрос к объекту по ключу <paramref name="storageKey"/>. Возвращает <c>null</c>,
    /// если объект не существует. Обработчик финализации модуля Files использует это для проверки
    /// размера и типа содержимого относительно заявленных значений перед переводом строки
    /// из состояния <c>PendingUpload</c>.
    /// </summary>
    Task<StoredObjectMetadata?> HeadObjectAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Вычисляет постоянный, не истекающий публичный URL объекта. Используется, когда
    /// <c>FileAsset</c> с <c>Visibility=Public</c> потребляется долгоживущей сохранённой ссылкой
    /// (например, <c>imageUrl</c> продукта), где предварительно подписанный 5-минутный URL
    /// истёк бы вскоре после сохранения.
    ///
    /// Бэкенды S3 строят этот URL из <c>PublicBaseUrl</c> (или S3-хоста бакета) и предполагают,
    /// что политика бакета разрешает публичное чтение объекта. Локальное хранилище возвращает
    /// путь относительно wwwroot источника API. Вызывающему коду, которому нужен доступ
    /// с проверкой прав, следует использовать <see cref="GenerateDownloadUrlAsync"/>.
    /// </summary>
    /// <remarks>
    /// Возвращает <c>string</c> намеренно — локальное хранилище формирует путь относительно
    /// сервера (разрешаемый позже клиентом относительно источника API), который не является
    /// корректным Uri.
    /// </remarks>
#pragma warning disable CA1055 // Uri vs string — см. примечание выше
    string BuildPublicUrl(string storageKey);
#pragma warning restore CA1055
}
