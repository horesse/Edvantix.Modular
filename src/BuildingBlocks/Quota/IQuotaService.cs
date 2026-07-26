using EDV.Framework.Shared.Quota;

namespace EDV.Framework.Quota;

public interface IQuotaService
{
    /// <summary>
    /// Проверяет, поместится ли <paramref name="amount"/> единиц ресурса <paramref name="resource"/>
    /// в текущую квоту арендатора. НЕ изменяет счётчик.
    /// </summary>
    ValueTask<QuotaCheckResult> CheckAsync(string tenantId, QuotaResource resource, long amount, CancellationToken ct = default);

    /// <summary>
    /// Увеличивает счётчик для <paramref name="resource"/> на <paramref name="amount"/> и
    /// возвращает новое суммарное использование за текущий период. Не выполняет проверку лимитов.
    /// </summary>
    ValueTask<long> RecordAsync(string tenantId, QuotaResource resource, long amount, CancellationToken ct = default);

    /// <summary>
    /// Атомарно проверяет и записывает за один шаг. Если лимит будет превышен, счётчик
    /// не увеличивается, а <see cref="QuotaCheckResult.Allowed"/> равно false.
    /// </summary>
    ValueTask<QuotaCheckResult> CheckAndRecordAsync(string tenantId, QuotaResource resource, long amount, CancellationToken ct = default);

    /// <summary>
    /// Возвращает текущее использование <paramref name="resource"/> в активном периоде.
    /// Для ресурсов на основе датчиков делегирует зарегистрированным экземплярам <see cref="IQuotaGaugeProvider"/>.
    /// </summary>
    ValueTask<long> GetCurrentAsync(string tenantId, QuotaResource resource, CancellationToken ct = default);
}

/// <summary>
/// Точка расширения: модули могут реализовать этот интерфейс для сообщения текущего использования
/// ресурсов на основе датчиков (например, StorageBytes, Users). Сервис квот будет вызывать
/// провайдера, соответствующего запрошенному ресурсу, по требованию.
/// </summary>
public interface IQuotaGaugeProvider
{
    QuotaResource Resource { get; }
    ValueTask<long> GetCurrentAsync(string tenantId, CancellationToken ct = default);
}