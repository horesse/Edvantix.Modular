using EDV.Framework.Quota;
using EDV.Framework.Shared.Quota;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EDV.Modules.Identity.Services;

/// <summary>
/// Сообщает текущее число пользователей арендатора как датчик квоты. Использует ограниченный
/// арендатором <see cref="IdentityDbContext"/>, поэтому провайдер отвечает только за разрешённого
/// в запросе арендатора; для любого другого id арендатора обращаемся к <see cref="UserManager{TUser}"/>
/// с обходом фильтра арендатора, чтобы избежать межарендаторной утечки счётчиков.
/// </summary>
internal sealed class UserCountQuotaGaugeProvider : IQuotaGaugeProvider
{
    private readonly UserManager<AppUser> _userManager;

    public UserCountQuotaGaugeProvider(UserManager<AppUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        _userManager = userManager;
    }

    public QuotaResource Resource => QuotaResource.Users;

    public async ValueTask<long> GetCurrentAsync(string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return await _userManager.Users
            .IgnoreQueryFilters()
            .CountAsync(u => EF.Property<string>(u, "TenantId") == tenantId, ct)
            .ConfigureAwait(false);
    }
}
