using System.Net;
using Finbuckle.MultiTenant.Abstractions;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Quota;
using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Shared.Quota;
using EDV.Framework.Shared.Storage;
using EDV.Framework.Storage.DTOs;
using EDV.Framework.Storage.Services;
using Microsoft.Extensions.Logging;

namespace EDV.Framework.Storage;

/// <summary>
/// Декорирует <see cref="IStorageService"/> так, чтобы каждая загрузка списывала с арендатора
/// показатель <see cref="QuotaResource.StorageBytes"/>, а каждое удаление возвращало его обратно.
/// Если у запроса не разрешён арендатор, работаем без учёта квот — это соответствует поведению
/// мидлвари, которая применяет ограничения только к трафику с арендатором. Сбои загрузки
/// откатывают счётчик назад, чтобы частичный PUT не оставил завышенный баланс.
/// </summary>
internal sealed class QuotaMeteredStorageService : IStorageService
{
    private readonly IStorageService _inner;
    private readonly IQuotaService _quotas;
    private readonly IMultiTenantContextAccessor<AppTenantInfo> _tenantAccessor;
    private readonly ILogger<QuotaMeteredStorageService> _logger;

    public QuotaMeteredStorageService(
        IStorageService inner,
        IQuotaService quotas,
        IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
        ILogger<QuotaMeteredStorageService> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(quotas);
        ArgumentNullException.ThrowIfNull(tenantAccessor);
        ArgumentNullException.ThrowIfNull(logger);

        _inner = inner;
        _quotas = quotas;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    public async Task<string> UploadAsync<T>(FileUploadRequest request, FileType fileType, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return await _inner.UploadAsync<T>(request, fileType, cancellationToken).ConfigureAwait(false);
        }

        var bytes = request.Data.Count;
        var check = await _quotas
            .CheckAndRecordAsync(tenantId, QuotaResource.StorageBytes, bytes, cancellationToken)
            .ConfigureAwait(false);

        if (!check.Allowed)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Загрузка отклонена для арендатора {TenantId} — превышена квота хранилища ({Current}/{Limit} байт)",
                    tenantId, check.CurrentUsage, check.Limit);
            }

            throw new CustomException(
                $"Превышена квота хранилища ({check.CurrentUsage}/{check.Limit} байт).",
                errors: null,
                HttpStatusCode.InsufficientStorage);
        }

        try
        {
            return await _inner.UploadAsync<T>(request, fileType, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Откатываем списание, чтобы неудачная запись не расходовала квоту навсегда.
            await _quotas
                .RecordAsync(tenantId, QuotaResource.StorageBytes, -bytes, CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
    }

    public Task<FileDownloadResponse?> DownloadAsync(string path, CancellationToken cancellationToken = default)
        => _inner.DownloadAsync(path, cancellationToken);

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
        => _inner.ExistsAsync(path, cancellationToken);

    public Task<long> GetSizeAsync(string path, CancellationToken cancellationToken = default)
        => _inner.GetSizeAsync(path, cancellationToken);

    public async Task RemoveAsync(string path, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;

        // Узнаём размер до удаления, чтобы списать точную сумму. Для отсутствующих объектов — 0.
        long size = 0;
        if (!string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(path))
        {
            size = await _inner.GetSizeAsync(path, cancellationToken).ConfigureAwait(false);
        }

        await _inner.RemoveAsync(path, cancellationToken).ConfigureAwait(false);

        if (size > 0 && !string.IsNullOrWhiteSpace(tenantId))
        {
            await _quotas
                .RecordAsync(tenantId, QuotaResource.StorageBytes, -size, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    // Выпуск предварительно подписанного URL и HEAD проходят без изменений — байты не перемещаются,
    // поэтому квота не затрагивается. Модуль Files списывает при финализации (размер из HEAD)
    // и возвращает при полной очистке (через RemoveAsync выше).
    public Task<PresignedUploadUrl> GenerateUploadUrlAsync(string storageKey, string contentType, long maxBytes, TimeSpan ttl, CancellationToken cancellationToken = default)
        => _inner.GenerateUploadUrlAsync(storageKey, contentType, maxBytes, ttl, cancellationToken);

    public Task<Uri> GenerateDownloadUrlAsync(string storageKey, TimeSpan ttl, string? responseContentDisposition = null, CancellationToken cancellationToken = default)
        => _inner.GenerateDownloadUrlAsync(storageKey, ttl, responseContentDisposition, cancellationToken);

    public Task<StoredObjectMetadata?> HeadObjectAsync(string storageKey, CancellationToken cancellationToken = default)
        => _inner.HeadObjectAsync(storageKey, cancellationToken);

    public string BuildPublicUrl(string storageKey) => _inner.BuildPublicUrl(storageKey);
}
