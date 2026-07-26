using Amazon.S3;
using Amazon.S3.Model;
using EDV.Framework.Shared.Storage;
using EDV.Framework.Storage.DTOs;
using EDV.Framework.Storage.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace EDV.Framework.Storage.S3;

internal sealed partial class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly S3StorageOptions _options;
    private readonly ILogger<S3StorageService> _logger;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider;

    private const string UploadBasePath = "uploads";

    // Сгенерировано на этапе компиляции, скомпилировано один раз — встроенные вызовы Regex.Replace
    // разбирали шаблон заново при каждой загрузке.
    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex FolderSanitizer();

    [GeneratedRegex(@"[^a-zA-Z0-9_\.-]")]
    private static partial Regex FileNameSanitizer();

    public S3StorageService(IAmazonS3 s3, IOptions<S3StorageOptions> options, ILogger<S3StorageService> logger)
    {
        _s3 = s3;
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        _contentTypeProvider = new FileExtensionContentTypeProvider();

        if (string.IsNullOrWhiteSpace(_options.Bucket))
        {
            throw new InvalidOperationException("Storage:S3:Bucket обязателен при использовании хранилища S3.");
        }
    }

    public async Task<string> UploadAsync<T>(FileUploadRequest request, FileType fileType, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(request);

        var rules = FileTypeMetadata.GetRules(fileType);
        var extension = Path.GetExtension(request.FileName);

        if (string.IsNullOrWhiteSpace(extension) || !rules.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Тип файла '{extension}' не разрешён. Разрешены: {string.Join(", ", rules.AllowedExtensions)}");
        }

        if (request.Data.Count > rules.MaxSizeInMB * 1024 * 1024)
        {
            throw new InvalidOperationException($"Файл превышает максимальный размер {rules.MaxSizeInMB} МБ.");
        }

        var key = BuildKey<T>(SanitizeFileName(request.FileName));

        using var stream = new MemoryStream([.. request.Data]);

        var putRequest = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = stream,
            ContentType = request.ContentType
        };

        // Полагаемся на политику бакета для публичного доступа; не устанавливаем ACL во избежание
        // конфликтов с бакетами, где ACL отключены.
        await _s3.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Файл загружен в бакет S3 {Bucket} с ключом {Key}", _options.Bucket, key);
        }

        return BuildPublicUrl(key);
    }

    public async Task RemoveAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var key = NormalizeKey(path);
            await _s3.DeleteObjectAsync(_options.Bucket, key, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка S3 при удалении объекта {Path}: {StatusCode}", path, ex.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Непредвиденная ошибка при удалении объекта S3 {Path}", path);
        }
    }

    public async Task<FileDownloadResponse?> DownloadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var key = NormalizeKey(path);
            var request = new GetObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key
            };

            var response = await _s3.GetObjectAsync(request, cancellationToken).ConfigureAwait(false);
            var fileName = Path.GetFileName(key);

            // Используем ContentType из ответа, если доступен, иначе определяем по расширению
            var contentType = response.Headers.ContentType;
            if (string.IsNullOrWhiteSpace(contentType) && !_contentTypeProvider.TryGetContentType(fileName, out contentType))
            {
                contentType = "application/octet-stream";
            }

            return new FileDownloadResponse
            {
                Stream = response.ResponseStream,
                ContentType = contentType!,
                FileName = fileName,
                ContentLength = response.ContentLength
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "Объект S3 не найден: {Path}", path);
            }
            return null;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка S3 при загрузке объекта {Path}: {StatusCode}", path, ex.StatusCode);
            return null;
        }
        // Запасной вариант для непредвиденных ошибок, не связанных с S3 (например, сеть, сериализация).
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Непредвиденная ошибка при загрузке объекта S3 {Path}", path);
            return null;
        }
    }

    public async Task<long> GetSizeAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return 0;
        }

        try
        {
            var key = NormalizeKey(path);
            var metadata = await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _options.Bucket,
                Key = key
            }, cancellationToken).ConfigureAwait(false);

            return metadata.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return 0;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка S3 при чтении размера объекта {Path}: {StatusCode}", path, ex.StatusCode);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Непредвиденная ошибка при чтении размера объекта S3: {Path}", path);
            return 0;
        }
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var key = NormalizeKey(path);
            var request = new GetObjectMetadataRequest
            {
                BucketName = _options.Bucket,
                Key = key
            };

            await _s3.GetObjectMetadataAsync(request, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка S3 при проверке существования объекта {Path}: {StatusCode}", path, ex.StatusCode);
            return false;
        }
        // Запасной вариант для непредвиденных ошибок, не связанных с S3 (например, сеть, конфигурация).
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Непредвиденная ошибка при проверке существования объекта S3: {Path}", path);
            return false;
        }
    }

    public async Task<PresignedUploadUrl> GenerateUploadUrlAsync(
        string storageKey,
        string contentType,
        long maxBytes,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var key = NormalizeKey(storageKey);
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = expiresAt.UtcDateTime,
            ContentType = contentType,
            Protocol = ResolvePresignProtocol()
        };

        var url = await _s3.GetPreSignedURLAsync(request).ConfigureAwait(false);

        var requiredHeaders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Content-Type"] = contentType
        };

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Выпущен предварительно подписанный PUT-URL для бакета {Bucket} ключа {Key}, истекает {ExpiresAt}",
                _options.Bucket, key, expiresAt);
        }

        return new PresignedUploadUrl(new Uri(url), requiredHeaders, expiresAt);
    }

    public async Task<Uri> GenerateDownloadUrlAsync(
        string storageKey,
        TimeSpan ttl,
        string? responseContentDisposition = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        var key = NormalizeKey(storageKey);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(ttl),
            Protocol = ResolvePresignProtocol()
        };

        if (!string.IsNullOrWhiteSpace(responseContentDisposition))
        {
            request.ResponseHeaderOverrides.ContentDisposition = responseContentDisposition;
        }

        var url = await _s3.GetPreSignedURLAsync(request).ConfigureAwait(false);
        return new Uri(url);
    }

    public async Task<StoredObjectMetadata?> HeadObjectAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return null;
        }

        try
        {
            var key = NormalizeKey(storageKey);
            var metadata = await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _options.Bucket,
                Key = key
            }, cancellationToken).ConfigureAwait(false);

            var contentType = string.IsNullOrWhiteSpace(metadata.Headers.ContentType)
                ? "application/octet-stream"
                : metadata.Headers.ContentType;

            var lastModified = metadata.LastModified.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(metadata.LastModified.Value, DateTimeKind.Utc), TimeSpan.Zero)
                : DateTimeOffset.UtcNow;

            return new StoredObjectMetadata(
                metadata.ContentLength,
                contentType,
                lastModified,
                metadata.ETag);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка HEAD-запроса S3 для {Key}: {StatusCode}", storageKey, ex.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Непредвиденная ошибка HEAD-запроса S3 для {Key}", storageKey);
            return null;
        }
    }

    // MinIO и другие S3-совместимые сервисы часто работают по обычному HTTP, но SDK по умолчанию
    // выпускает предварительно подписанные URL по HTTPS независимо от схемы ServiceURL (там их
    // нельзя использовать для PUT). Определяем протокол по ServiceURL.
    private Protocol ResolvePresignProtocol()
    {
        if (!string.IsNullOrWhiteSpace(_options.ServiceUrl)
            && Uri.TryCreate(_options.ServiceUrl, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return Protocol.HTTP;
        }
        return Protocol.HTTPS;
    }

    private string BuildKey<T>(string fileName) where T : class
    {
        var folder = FolderSanitizer().Replace(typeof(T).Name.ToLowerInvariant(), "_");
        var relativePath = Path.Combine(UploadBasePath, folder, $"{Guid.NewGuid():N}_{fileName}").Replace("\\", "/", StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(_options.Prefix))
        {
            return $"{_options.Prefix.TrimEnd('/')}/{relativePath}";
        }

        return relativePath;
    }

    public string BuildPublicUrl(string storageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        var key = NormalizeKey(storageKey);
        var safeKey = key.TrimStart('/');

        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return $"{_options.PublicBaseUrl.TrimEnd('/')}/{safeKey}";
        }

        // S3-совместимый эндпоинт с path-style адресацией (MinIO и подобные).
        if (!string.IsNullOrWhiteSpace(_options.ServiceUrl))
        {
            return $"{_options.ServiceUrl.TrimEnd('/')}/{_options.Bucket}/{safeKey}";
        }

        if (!_options.PublicRead)
        {
            return key;
        }

        if (string.IsNullOrWhiteSpace(_options.Region) || string.Equals(_options.Region, "us-east-1", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://{_options.Bucket}.s3.amazonaws.com/{safeKey}";
        }

        return $"https://{_options.Bucket}.s3.{_options.Region}.amazonaws.com/{safeKey}";
    }

    private string NormalizeKey(string path)
    {
        // Если передан полный URL, отбрасываем хост и строку запроса, чтобы получить ключ объекта.
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
        {
            path = uri.AbsolutePath;
        }

        var trimmed = path.TrimStart('/');
        if (!string.IsNullOrWhiteSpace(_options.Prefix) && trimmed.StartsWith(_options.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (!string.IsNullOrWhiteSpace(_options.Prefix))
        {
            return $"{_options.Prefix.TrimEnd('/')}/{trimmed}";
        }

        return trimmed;
    }

    private static string SanitizeFileName(string fileName)
    {
        return FileNameSanitizer().Replace(fileName, "_");
    }
}
