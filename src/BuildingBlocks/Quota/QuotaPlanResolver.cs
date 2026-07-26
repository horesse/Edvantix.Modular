using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Shared.Quota;

namespace EDV.Framework.Quota;

/// <summary>
/// Определяет эффективный лимит для заданных арендатора и ресурса. Локальные переопределения арендатора
/// из <see cref="AppTenantInfo.QuotaLimits"/> имеют приоритет; в противном случае используется каталог тарифов
/// из <see cref="QuotaOptions.Plans"/> (с откатом к <see cref="QuotaOptions.DefaultPlan"/>).
/// Возвращает <see cref="long.MaxValue"/>, если лимит не применяется.
/// </summary>
public sealed class QuotaPlanResolver
{
    private readonly QuotaOptions _options;

    public QuotaPlanResolver(QuotaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public long ResolveLimit(AppTenantInfo? tenant, QuotaResource resource)
    {
        if (tenant is not null && tenant.QuotaLimits.TryGetValue(resource, out var tenantLimit))
        {
            return NormalizeLimit(tenantLimit);
        }

        var planName = !string.IsNullOrWhiteSpace(tenant?.Plan) ? tenant!.Plan! : _options.DefaultPlan;

        if (_options.Plans.TryGetValue(planName, out var plan)
            && plan.TryGetValue(resource, out var planLimit))
        {
            return NormalizeLimit(planLimit);
        }

        // Откат к тарифу по умолчанию, если тариф арендатора отсутствует в каталоге.
        if (!string.Equals(planName, _options.DefaultPlan, StringComparison.OrdinalIgnoreCase)
            && _options.Plans.TryGetValue(_options.DefaultPlan, out var defaultPlan)
            && defaultPlan.TryGetValue(resource, out var defaultLimit))
        {
            return NormalizeLimit(defaultLimit);
        }

        return long.MaxValue;
    }

    private static long NormalizeLimit(long value) => value < 0 ? long.MaxValue : value;
}