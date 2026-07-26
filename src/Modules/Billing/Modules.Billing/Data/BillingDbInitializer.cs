using EDV.Framework.Persistence;
using EDV.Modules.Billing.Contracts;
using EDV.Modules.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Billing.Data;

public sealed class BillingDbInitializer(
    BillingDbContext dbContext,
    ILogger<BillingDbInitializer> logger) : IDbInitializer
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("[Billing] миграции применены");
        }
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        // Тарифы — это глобальный каталог (IGlobalEntity); начальные данные заполняются один раз.
        // "free" служит запасным вариантом для пробного периода; ключи совпадают с ключами тарифов
        // из QuotaOptions, чтобы корректно разрешались лимиты квот.
        if (await dbContext.Plans.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        dbContext.Plans.Add(BillingPlan.Create("free", "Free", "USD", 0m, interval: PlanInterval.Monthly));
        dbContext.Plans.Add(BillingPlan.Create("pro", "Pro", "USD", 29m, interval: PlanInterval.Monthly));
        dbContext.Plans.Add(BillingPlan.Create("pro-annual", "Pro (Annual)", "USD", 29m,
            interval: PlanInterval.Yearly, annualPrice: 290m));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[Billing] заполнены тарифы по умолчанию (free, pro, pro-annual)");
    }
}
