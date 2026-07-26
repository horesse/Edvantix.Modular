using EDV.Framework.Shared.Storage;
using EDV.Framework.Storage.DTOs;
using EDV.Framework.Storage.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using System.Text.RegularExpressions;
using System.Threading;

namespace EDV.Framework.Storage.Local;

public sealed partial class LocalStorageService : IStorageService
{
    private const string UploadBasePath = "uploads";

    // Сгенерировано на этапе компиляции, скомпилировано один раз — встроенные вызовы Regex.Replace
    // разбирали шаблон заново при каждой загрузке.
    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex FolderSanitizer();

    [GeneratedRegex(@"[^a-zA-Z0-9_\.-]")]
    private static partial Regex FileNameSanitizer();
    private readonly string _rootPath;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider;

    public LocalStorageService(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _rootPath = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;
        _contentTypeProvider = new FileExtensionContentTypeProvider();
    }

    public async Task<string> UploadAsync<T>(FileUploadRequest request, FileType fileType, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(request);

        var rules = FileTypeMetadata.GetRules(fileType);
        var extension = Path.GetExtension(request.FileName);

        if (string.IsNullOrWhiteSpace(extension) ||
            !rules.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Тип файла '{extension}' не разрешён. Разрешены: {string.Join(", ", rules.AllowedExtensions)}");
        }

        if (request.Data.Count > rules.MaxSizeInMB * 1024 * 1024)
        {
            throw new InvalidOperationException($"Файл превышает максимальный размер {rules.MaxSizeInMB} МБ.");
        }

#pragma warning disable CA1308 // имена папок намеренно в нижнем регистре для URL/путей
        var folder = FolderSanitizer().Replace(typeof(T).Name.ToLowerInvariant(), "_");
#pragma warning restore CA1308
        var safeFileName = $"{Guid.NewGuid():N}_{SanitizeFileName(request.FileName)}";
        var relativePath = Path.Combine(UploadBasePath, folder, safeFileName);
        var fullPath = Path.Combine(_rootPath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await File.WriteAllBytesAsync(fullPath, request.Data.ToArray(), cancellationToken);

        return relativePath.Replace("\\", "/", StringComparison.Ordinal); // Нормализация для URL
    }

    public Task<FileDownloadResponse?> DownloadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult<FileDownloadResponse?>(null);
        }

        var normalizedPath = path.Replace("/", Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal);
        var fullPath = Path.Combine(_rootPath, normalizedPath);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<FileDownloadResponse?>(null);
        }

        var fileInfo = new FileInfo(fullPath);
        var fileName = Path.GetFileName(fullPath);

        if (!_contentTypeProvider.TryGetContentType(fileName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);

        return Task.FromResult<FileDownloadResponse?>(new FileDownloadResponse
        {
            Stream = stream,
            ContentType = contentType,
            FileName = fileName,
            ContentLength = fileInfo.Length
        });
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(false);
        }

        var normalizedPath = path.Replace("/", Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal);
        var fullPath = Path.Combine(_rootPath, normalizedPath);

        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<long> GetSizeAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(0L);
        }

        var normalizedPath = path.Replace("/", Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal);
        var fullPath = Path.Combine(_rootPath, normalizedPath);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult(0L);
        }

        return Task.FromResult(new FileInfo(fullPath).Length);
    }

    public Task RemoveAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) return Task.CompletedTask;

        var fullPath = Path.Combine(_rootPath, path);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private static string SanitizeFileName(string fileName)
    {
        return FileNameSanitizer().Replace(fileName, "_");
    }

    // Резервный вариант подписи для разработки, когда Storage:Provider != s3 (в продакшне используется
    // S3StorageService). Хранилище токенов статично на уровне процесса, чтобы dev-мидлварь могла
    // потребить токен без повторного разрешения через DI.
    private static LocalPresignTokenStore? _staticTokenStore;
    public static LocalPresignTokenStore SharedTokenStore => LazyInitializer.EnsureInitialized(ref _staticTokenStore);

    public Task<PresignedUploadUrl> GenerateUploadUrlAsync(
        string storageKey, string contentType, long maxBytes, TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var token = SharedTokenStore.Issue(storageKey, contentType, maxBytes, ttl);
        var url = new Uri($"local://upload/{token}", UriKind.Absolute);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Content-Type"] = contentType };
        return Task.FromResult(new PresignedUploadUrl(url, headers, DateTimeOffset.UtcNow.Add(ttl)));
    }

    public Task<Uri> GenerateDownloadUrlAsync(
        string storageKey, TimeSpan ttl, string? responseContentDisposition = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        // Локальный режим отдаёт файлы из /wwwroot — подпись не требуется.
        var normalized = storageKey.TrimStart('/').Replace("\\", "/", StringComparison.Ordinal);
        return Task.FromResult(new Uri($"/{normalized}", UriKind.Relative));
    }

#pragma warning disable CA1055 // возвращает путь относительно сервера, а не корректный Uri — см. IStorageService.BuildPublicUrl
    public string BuildPublicUrl(string storageKey)
#pragma warning restore CA1055
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        var normalized = storageKey.TrimStart('/').Replace("\\", "/", StringComparison.Ordinal);
        // Разрешается позже относительно источника API дашборда (как UserProfileService делает
        // для устаревших аватаров). Ведущий слэш позволяет клиентам отличать абсолютные URL
        // от относительных серверу.
        return $"/{normalized}";
    }

    public Task<StoredObjectMetadata?> HeadObjectAsync(
        string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Task.FromResult<StoredObjectMetadata?>(null);
        }

        var normalizedPath = storageKey.Replace("/", Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal);
        var fullPath = Path.Combine(_rootPath, normalizedPath);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<StoredObjectMetadata?>(null);
        }

        var info = new FileInfo(fullPath);
        if (!_contentTypeProvider.TryGetContentType(info.Name, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return Task.FromResult<StoredObjectMetadata?>(new StoredObjectMetadata(
            info.Length,
            contentType!,
            new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
            ETag: null));
    }
}
