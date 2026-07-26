using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Shared.Persistence;
using EDV.Modules.Multitenancy.Contracts.Dtos;
using EDV.Modules.Multitenancy.Contracts.v1.GetTenants;

namespace EDV.Modules.Multitenancy.Contracts;

public interface ITenantService
{
    Task<PagedResponse<TenantDto>> GetAllAsync(GetTenantsQuery query, CancellationToken cancellationToken);

    Task<bool> ExistsWithIdAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> ExistsWithNameAsync(string name, CancellationToken cancellationToken = default);

    Task<TenantStatusDto> GetStatusAsync(string id, CancellationToken cancellationToken = default);

    Task<string> CreateAsync(string id, string name, string? connectionString, string adminEmail, string? issuer, string planKey, DateTime validUpto, CancellationToken cancellationToken);

    Task<string> ActivateAsync(string id, CancellationToken cancellationToken);

    Task<string> DeactivateAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Продлевает срок действия тенанта на один срок плана, складывая с оставшимся временем (без сдвига
    /// назад), и переключает план тенанта, если <paramref name="newPlanKey"/> отличается. Возвращает
    /// применённое окно срока и признак изменения плана, чтобы вызывающий код мог опубликовать
    /// соответствующее событие продления.
    /// </summary>
    Task<(DateTime PeriodStartUtc, DateTime ValidUpto, bool PlanChanged)> RenewAsync(
        string id, string newPlanKey, int termMonths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Переопределение оператором, устанавливающее срок действия тенанта на явно заданную дату без
    /// побочных эффектов на биллинг (без подписки, счёта, события продления) — для бесплатных периодов,
    /// продления в рамках поддержки или немедленного истечения срока. В отличие от <see cref="RenewAsync"/>,
    /// может сдвигать дату назад. Возвращает применённое значение <c>ValidUpto</c> (UTC).
    /// </summary>
    Task<DateTime> AdjustValidityAsync(string id, DateTime validUpto, CancellationToken cancellationToken = default);

    Task MigrateTenantAsync(AppTenantInfo tenant, CancellationToken cancellationToken);

    Task SeedTenantAsync(AppTenantInfo tenant, CancellationToken cancellationToken);
}