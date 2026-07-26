using EDV.Framework.Eventing.Abstractions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Multitenancy.Contracts.Events;
using EDV.Modules.Multitenancy.Data;
using EDV.Modules.Multitenancy.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EDV.Modules.Multitenancy.Services;

/// <summary>
/// Ежедневное сканирование, уведомляющее тенантов о приближении или истечении <c>ValidUpto</c>. Для каждого
/// активного некорневого тенанта определяется состояние (скоро истечёт / в льготном периоде / истёк),
/// записывается строка дедупликации в <see cref="TenantExpiryNotice"/> (одна на комбинацию тенант+состояние+
/// период действия) и публикуется соответствующее интеграционное событие. Побочные эффекты уведомления
/// (отправка email) обрабатываются потребителями события.
/// </summary>
public sealed class TenantExpiryScanJob
{
    private readonly IMultiTenantStore<AppTenantInfo> _tenantStore;
    private readonly TenantDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly IMultiTenantContextSetter _tenantContextSetter;
    private readonly TimeProvider _timeProvider;
    private readonly TenantBillingOptions _options;
    private readonly ILogger<TenantExpiryScanJob> _logger;

    public TenantExpiryScanJob(
        IMultiTenantStore<AppTenantInfo> tenantStore,
        TenantDbContext db,
        IEventBus eventBus,
        IMultiTenantContextSetter tenantContextSetter,
        TimeProvider timeProvider,
        IOptions<TenantBillingOptions> options,
        ILogger<TenantExpiryScanJob> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _tenantStore = tenantStore;
        _db = db;
        _eventBus = eventBus;
        _tenantContextSetter = tenantContextSetter;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var tenants = await _tenantStore.GetAllAsync().ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var published = 0;
        foreach (var tenant in tenants)
        {
            if (!tenant.IsActive ||
                string.Equals(tenant.Id, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                if (await TryNotifyAsync(tenant, now, cancellationToken).ConfigureAwait(false))
                {
                    published++;
                }
            }
#pragma warning disable CA1031 // Сбой одного тенанта не должен блокировать остальную часть сканирования
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "[Multitenancy] сканирование истечения срока действия завершилось ошибкой для тенанта {TenantId}", tenant.Id);
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[Multitenancy] сканирование истечения срока действия опубликовало {Count} уведомление(й)", published);
        }
    }

    private async Task<bool> TryNotifyAsync(AppTenantInfo tenant, DateTime now, CancellationToken ct)
    {
        var validUpto = tenant.ValidUpto;
        var graceEnds = validUpto.AddDays(_options.GracePeriodDays);

        string noticeType;
        if (now > graceEnds)
        {
            noticeType = TenantExpiryNoticeTypes.Expired;
        }
        else if (now > validUpto)
        {
            noticeType = TenantExpiryNoticeTypes.EnteredGrace;
        }
        else if (now >= validUpto.AddDays(-_options.ExpiryNotificationLeadDays))
        {
            noticeType = TenantExpiryNoticeTypes.NearingExpiry;
        }
        else
        {
            return false; // в норме и вне окна напоминания
        }

        // Дедупликация: одно уведомление на тенанта для каждого состояния в рамках периода действия
        // (перезапускается при изменении ValidUpto).
        var alreadyNotified = await _db.TenantExpiryNotices
            .AnyAsync(x => x.TenantId == tenant.Id && x.NoticeType == noticeType && x.ValidUptoUtc == validUpto, ct)
            .ConfigureAwait(false);
        if (alreadyNotified)
        {
            return false;
        }

        _db.TenantExpiryNotices.Add(TenantExpiryNotice.Record(tenant.Id, noticeType, validUpto, now));
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Устанавливаем контекст Finbuckle перед публикацией: последующие обработчики (например, рассылка
        // вебхуков) используют DbContext-ы с фильтрацией по тенанту, которые без него выбросят NRE,
        // поскольку у фонового задания нет HTTP-запроса.
        _tenantContextSetter.MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        await _eventBus.PublishAsync(BuildEvent(noticeType, tenant, validUpto, graceEnds, now), ct).ConfigureAwait(false);
        return true;
    }

    private static IIntegrationEvent BuildEvent(
        string noticeType, AppTenantInfo tenant, DateTime validUpto, DateTime graceEnds, DateTime now)
    {
        var id = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString();
        const string source = "Multitenancy";
        var name = tenant.Name ?? tenant.Id;
        var email = tenant.AdminEmail;

        return noticeType switch
        {
            TenantExpiryNoticeTypes.NearingExpiry => new TenantNearingExpiryIntegrationEvent(
                id, now, tenant.Id, correlationId, source, name, email, tenant.Plan, validUpto, graceEnds,
                DaysRemaining: Math.Max(0, (int)Math.Ceiling((validUpto - now).TotalDays))),
            TenantExpiryNoticeTypes.EnteredGrace => new TenantEnteredGraceIntegrationEvent(
                id, now, tenant.Id, correlationId, source, name, email, tenant.Plan, validUpto, graceEnds),
            _ => new TenantExpiredIntegrationEvent(
                id, now, tenant.Id, correlationId, source, name, email, tenant.Plan, validUpto, graceEnds),
        };
    }
}
