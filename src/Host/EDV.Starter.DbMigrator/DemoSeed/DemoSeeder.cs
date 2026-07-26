using EDV.Framework.Shared.Identity;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Contracts;
using EDV.Modules.Billing.Data;
using EDV.Modules.Billing.Domain;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using EDV.Modules.Multitenancy.Contracts;
using EDV.Modules.Multitenancy.Data;
using EDV.Modules.Multitenancy.Provisioning;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace EDV.Starter.DbMigrator.DemoSeed;

/// <summary>
/// Отвечает за «насыщенный демо-контент», который нужен окружению разработки, чтобы
/// выглядеть живым: арендаторы <c>acme</c> и <c>globex</c>, их демо-пользователи,
/// пользовательские роли, контент каталога, заявки и чат. Вызывается глаголом
/// мигратора <c>seed-demo</c> — никогда во время выполнения API.
///
/// Идемпотентно: каждый шаг проверяет состояние перед записью, поэтому повторный
/// запуск глагола на уже заполненной базе данных ничего не меняет.
///
/// Именование: до 2026-05-17 это находилось в API как <c>DevDataSeeder</c>
/// (размещённый сервис) — перенесено сюда, чтобы API больше не изменял данные
/// при запуске, следуя тому же принципу, по которому миграции были вынесены в
/// этот проект. См. <c>docs/superpowers/specs/2026-05-14-remove-api-auto-migration-design.md</c>.
/// </summary>
internal sealed class DemoSeeder
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<DemoSeeder> _logger;
    private string _sharedPassword = string.Empty;

    public static readonly DemoTenant Acme = new(
        Id: "acme",
        Name: "Acme Corp",
        AdminEmail: "admin@acme.com",
        Issuer: "edv.demo.acme",
        PlanKey: "pro-annual");

    public static readonly DemoTenant Globex = new(
        Id: "globex",
        Name: "Globex",
        AdminEmail: "admin@globex.com",
        Issuer: "edv.demo.globex",
        PlanKey: "free");

    public DemoSeeder(IServiceProvider services, IConfiguration config, ILogger<DemoSeeder> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // Берётся из конфигурации, чтобы демо-учётные данные не были захардкожены.
        _sharedPassword = _config["Seed:DemoPassword"]
            ?? throw new InvalidOperationException(
                "Seed:DemoPassword должен быть настроен (см. appsettings.Development.json).");

        await EnsureDemoTenantsExistAsync(cancellationToken).ConfigureAwait(false);
        await SeedRootSuperAdminAsync(cancellationToken).ConfigureAwait(false);

        foreach (var demo in new[] { Acme, Globex })
        {
            await SeedTenantSubscriptionAsync(demo, cancellationToken).ConfigureAwait(false);
            await SeedTenantUsersAsync(demo, cancellationToken).ConfigureAwait(false);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "[demo-seed] завершено · корневой суперадминистратор + {Acme} + {Globex} заполнены пользователями / каталогом / заявками / чатом",
                Acme.Id, Globex.Id);
        }
    }

    // ─── Провижининг арендаторов ────────────────────────────────────────

    /// <summary>
    /// Добавляет демо-арендаторов в каталог, если их там нет, а затем проводит их
    /// через тот же путь миграции + заполнения <see cref="ITenantService"/>, что
    /// использует среда выполнения. Сервис провижининга внутри мигратора переходит
    /// на встроенное выполнение, поскольку Hangfire здесь не запущен — мы получаем
    /// синхронное подтверждение «арендатор готов» до возврата из этого метода.
    /// </summary>
    private async Task EnsureDemoTenantsExistAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var tenantStore = scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
        var tenantDb = scope.ServiceProvider.GetRequiredService<TenantDbContext>();

        foreach (var demo in new[] { Acme, Globex })
        {
            var existing = await tenantStore.GetAsync(demo.Id).ConfigureAwait(false);
            if (existing is null)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("[demo-seed] создание арендатора '{TenantId}'", demo.Id);
                }
                var tenant = new AppTenantInfo(demo.Id, demo.Name, connectionString: string.Empty, demo.AdminEmail, demo.Issuer);
                tenant.SetValidity(DateTime.UtcNow.AddYears(1));
                await tenantDb.TenantInfo.AddAsync(tenant, cancellationToken).ConfigureAwait(false);
                await tenantDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                existing = tenant;
            }

            // Тот же путь для каждого арендатора, что использует глагол apply мигратора. Инициализатор
            // Identity создаёт администратора арендатора, а инициализаторы Catalog/Tickets/Chat сегодня ничего не делают.
            await tenantService.MigrateTenantAsync(existing, cancellationToken).ConfigureAwait(false);
            await tenantService.SeedTenantAsync(existing, cancellationToken).ConfigureAwait(false);

            await EnsureProvisioningRecordAsync(tenantDb, demo.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Демо-арендаторы мигрируются и заполняются встроенно (см. выше), минуя конвейер
    /// провижининга — поэтому строка <see cref="TenantProvisioning"/> не существует, и
    /// панель администратора Provisioning вернула бы 404. Записываем завершённый запуск
    /// (все шаги выполнены), чтобы панель показывала реальную историю «Завершено».
    /// Идемпотентно: пропускается, если строка для арендатора уже существует.
    /// </summary>
    private static async Task EnsureProvisioningRecordAsync(TenantDbContext tenantDb, string tenantId, CancellationToken cancellationToken)
    {
        var alreadyTracked = await tenantDb.Set<TenantProvisioning>()
            .AnyAsync(p => p.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (alreadyTracked)
        {
            return;
        }

        var provisioning = new TenantProvisioning(tenantId, Guid.NewGuid().ToString());
        foreach (var step in Enum.GetValues<TenantProvisioningStepName>())
        {
            var stepEntity = new TenantProvisioningStep(provisioning.Id, step);
            stepEntity.MarkRunning();
            stepEntity.MarkCompleted();
            provisioning.Steps.Add(stepEntity);
        }
        provisioning.MarkCompleted();

        tenantDb.Add(provisioning);
        await tenantDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // ─── Подписка ──────────────────────────────────────────────────────

    /// <summary>
    /// Привязывает активную биллинговую <see cref="Subscription"/> к демо-арендатору, чтобы
    /// карточки PLAN / подписки на дашборде были заполнены «из коробки». Реальный путь создания
    /// арендатора делает это через <c>TenantSubscribedIntegrationEvent</c>, но демо-арендаторы
    /// провижинятся встроенно (см. <see cref="EnsureDemoTenantsExistAsync"/>) и никогда не
    /// публикуют это событие — поэтому мы записываем строку напрямую.
    ///
    /// Платные тарифы также получают выставленный счёт за период, как в реальном потоке. Он
    /// записывается напрямую, а не через <c>IBillingService</c>, чтобы не публиковать
    /// <c>InvoiceIssuedIntegrationEvent</c> — у одноразового мигратора нет диспетчера outbox,
    /// и заполнение демо-данными не должно рассылать уведомления/письма. Период подписки
    /// выравнивается по <c>ValidUpto</c> арендатора, чтобы период на дашборде совпадал с
    /// действующим окном валидности.
    ///
    /// Идемпотентно: пропускается, если у арендатора уже есть активная подписка.
    /// </summary>
    private async Task SeedTenantSubscriptionAsync(DemoTenant demo, CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var tenantStore = scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var tenant = await tenantStore.GetAsync(demo.Id).ConfigureAwait(false);
        if (tenant is null) return;

        // BillingDbContext НЕ фильтруется по арендатору (TenantId — явная колонка), поэтому
        // манипуляции с контекстом Finbuckle не требуются — фильтруем по TenantId напрямую.
        var billingDb = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var plan = await billingDb.Plans
            .FirstOrDefaultAsync(p => p.Key == demo.PlanKey && p.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (plan is null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "[demo-seed] [{Tenant}] тариф '{PlanKey}' не найден — подписка пропускается", demo.Id, demo.PlanKey);
            }
            return;
        }

        // Переиспользуем период уже существующей активной подписки, если он есть, чтобы повторные
        // запуски не создавали новую подписку, но при этом дозаполняли отсутствующий счёт; иначе
        // начинаем заново, выравниваясь по ValidUpto арендатора.
        var existing = await billingDb.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == demo.Id && s.Status == SubscriptionStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        var startUtc = existing?.StartUtc ?? DateTime.UtcNow;
        var endUtc = existing?.EndUtc ?? DateTime.SpecifyKind(tenant.ValidUpto, DateTimeKind.Utc);

        if (existing is null)
        {
            billingDb.Subscriptions.Add(Subscription.Create(demo.Id, plan.Id, startUtc, endUtc));
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "[demo-seed] [{Tenant}] оформлена подписка на тариф '{PlanKey}' (период заканчивается {End:o})",
                    demo.Id, plan.Key, endUtc);
            }
        }

        // Платные тарифы получают выставленный счёт за период (как реальный CreateTenant), записанный
        // напрямую, чтобы не публиковать InvoiceIssuedIntegrationEvent (нет диспетчера outbox; заполнение
        // не должно рассылать письма). Идемпотентно по номеру счёта; бесплатные тарифы (цена периода 0)
        // счёт не получают, как в продакшене.
        if (plan.TermPrice.Amount > 0m)
        {
            var invoiceNumber = string.Create(
                CultureInfo.InvariantCulture, $"SUB-{startUtc:yyyyMM}-{demo.Id.ToUpperInvariant()}");
            var invoiceExists = await billingDb.Invoices
                .AnyAsync(i => i.TenantId == demo.Id && i.InvoiceNumber == invoiceNumber, cancellationToken)
                .ConfigureAwait(false);
            if (!invoiceExists)
            {
                var invoice = Invoice.CreateDraft(
                    demo.Id, invoiceNumber, startUtc.Year, startUtc.Month, plan.Currency,
                    InvoicePurpose.Subscription, startUtc, endUtc);
                invoice.AddLineItem(
                    InvoiceLineItemKind.BaseFee,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{plan.Name} — {plan.Interval} subscription ({startUtc:yyyy-MM-dd} to {endUtc:yyyy-MM-dd})"),
                    1m,
                    plan.TermPrice.Amount);
                invoice.Issue();
                billingDb.Invoices.Add(invoice);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "[demo-seed] [{Tenant}] выставлен счёт за период {InvoiceNumber} ({Amount} {Currency})",
                        demo.Id, invoiceNumber, plan.TermPrice, plan.Currency);
                }
            }
        }

        await billingDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // ─── Пользователи и роли ─────────────────────────────────────────────

    private async Task SeedRootSuperAdminAsync(CancellationToken cancellationToken)
    {
        var rootTenant = new AppTenantInfo(
            id: MultitenancyConstants.Root.Id,
            name: MultitenancyConstants.Root.Name,
            connectionString: string.Empty,
            adminEmail: MultitenancyConstants.Root.EmailAddress,
            issuer: MultitenancyConstants.Root.Issuer);

        await SeedUsersInTenantAsync(rootTenant, BuildRootUsers(), [], cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedTenantUsersAsync(DemoTenant demo, CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var tenantStore = scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var tenant = await tenantStore.GetAsync(demo.Id).ConfigureAwait(false);
        if (tenant is null) return;

        var users = demo.Id == Acme.Id ? BuildAcmeUsers() : BuildGlobexUsers();
        var customRoles = demo.Id == Acme.Id ? BuildAcmeCustomRoles() : Array.Empty<DemoRole>();
        await SeedUsersInTenantAsync(tenant, users, customRoles, cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedUsersInTenantAsync(
        AppTenantInfo tenant,
        IReadOnlyList<DemoUser> users,
        IReadOnlyList<DemoRole> customRoles,
        CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = new PasswordHasher<AppUser>();

        foreach (var demoRole in customRoles)
        {
            var role = await roleManager.FindByNameAsync(demoRole.Name).ConfigureAwait(false);
            if (role is null)
            {
                role = new AppRole(demoRole.Name, demoRole.Description);
                await roleManager.CreateAsync(role).ConfigureAwait(false);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("[demo-seed] [{Tenant}] создана пользовательская роль '{Role}'", tenant.Id, demoRole.Name);
                }
            }

            var existingClaims = await roleManager.GetClaimsAsync(role).ConfigureAwait(false);
            foreach (var permission in demoRole.Permissions)
            {
                if (existingClaims.Any(c => c.Type == ClaimConstants.Permission && c.Value == permission))
                {
                    continue;
                }
                context.RoleClaims.Add(new AppRoleClaim
                {
                    RoleId = role.Id,
                    ClaimType = ClaimConstants.Permission,
                    ClaimValue = permission,
                    CreatedBy = "DemoSeeder",
                    CreatedOn = DateTimeOffset.UtcNow,
                });
            }
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var demoUser in users)
        {
            var existing = await userManager.FindByEmailAsync(demoUser.Email).ConfigureAwait(false);
            if (existing is null)
            {
                var user = new AppUser
                {
                    UserName = demoUser.UserName,
                    Email = demoUser.Email,
                    EmailConfirmed = true,
                    FirstName = demoUser.FirstName,
                    LastName = demoUser.LastName,
                    IsActive = true,
                    NormalizedEmail = demoUser.Email.ToUpperInvariant(),
                    NormalizedUserName = demoUser.UserName.ToUpperInvariant(),
                };
                user.PasswordHash = hasher.HashPassword(user, _sharedPassword);
                var created = await userManager.CreateAsync(user).ConfigureAwait(false);
                if (!created.Succeeded)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(
                            "[demo-seed] [{Tenant}] не удалось создать '{Email}': {Errors}",
                            tenant.Id, demoUser.Email,
                            string.Join("; ", created.Errors.Select(e => e.Description)));
                    }
                    continue;
                }
                existing = user;
            }
            else
            {
                await EnsureSharedPasswordAsync(userManager, hasher, existing).ConfigureAwait(false);
            }

            foreach (var role in demoUser.Roles)
            {
                if (!await userManager.IsInRoleAsync(existing, role).ConfigureAwait(false))
                {
                    var roleEntity = await roleManager.FindByNameAsync(role).ConfigureAwait(false);
                    if (roleEntity is null) continue;
                    await userManager.AddToRoleAsync(existing, role).ConfigureAwait(false);
                }
            }
        }

        // Администратор арендатора (admin@<tenant>.com) был создан IdentityDbInitializer с паролем по умолчанию
        // фреймворка. Приводим его к общему паролю, чтобы учётные данные, заявленные на панели входа для
        // разработки, соответствовали действительности.
        if (!string.IsNullOrWhiteSpace(tenant.AdminEmail))
        {
            var admin = await userManager.FindByEmailAsync(tenant.AdminEmail).ConfigureAwait(false);
            if (admin is not null)
            {
                await EnsureSharedPasswordAsync(userManager, hasher, admin).ConfigureAwait(false);
            }
        }
    }

    private async Task EnsureSharedPasswordAsync(
        UserManager<AppUser> userManager,
        PasswordHasher<AppUser> hasher,
        AppUser user)
    {
        if (await userManager.CheckPasswordAsync(user, _sharedPassword).ConfigureAwait(false))
        {
            return;
        }
        user.PasswordHash = hasher.HashPassword(user, _sharedPassword);
        var result = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded && _logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(
                "[demo-seed] не удалось сбросить пароль для '{Email}': {Errors}",
                user.Email,
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    // ─── Структуры демо-контента ────────────────────────────────────────

    internal sealed record DemoTenant(string Id, string Name, string AdminEmail, string Issuer, string PlanKey);
    internal sealed record DemoUser(
        string UserName,
        string Email,
        string FirstName,
        string LastName,
        IReadOnlyList<string> Roles);
    internal sealed record DemoRole(string Name, string Description, IReadOnlyList<string> Permissions);
    
    private static IReadOnlyList<DemoUser> BuildRootUsers() =>
    [
        new("superadmin", "superadmin@root.com", "Super", "Admin", [RoleConstants.Admin]),
    ];

    private static IReadOnlyList<DemoUser> BuildAcmeUsers() =>
    [
        new("acme.manager",  "manager@acme.com",  "Maya",   "Lin",      ["Manager"]),
        new("acme.support",  "support@acme.com",  "Sam",    "Rivera",   ["Support"]),
        new("acme.alice",    "alice@acme.com",    "Alice",  "Nguyen",   [RoleConstants.Basic]),
        new("acme.bob",      "bob@acme.com",      "Bob",    "Patel",    [RoleConstants.Basic]),
        new("acme.carol",    "carol@acme.com",    "Carol",  "Smith",    [RoleConstants.Basic]),
        new("acme.dan",      "dan@acme.com",      "Dan",    "Mueller",  [RoleConstants.Basic]),
        new("acme.erin",     "erin@acme.com",     "Erin",   "Okafor",   [RoleConstants.Basic]),
        new("acme.frank",    "frank@acme.com",    "Frank",  "Tanaka",   [RoleConstants.Basic]),
        new("acme.gina",     "gina@acme.com",     "Gina",   "Kowalski", [RoleConstants.Basic]),
        new("acme.henry",    "henry@acme.com",    "Henry",  "Park",     [RoleConstants.Basic]),
    ];

    private static IReadOnlyList<DemoUser> BuildGlobexUsers() =>
    [
        new("globex.dave",   "dave@globex.com",   "Dave",   "Hartwell", [RoleConstants.Basic]),
    ];

    // Claim'ы разрешений ссылаются на константы контрактов модуля — никогда не сырые строки.
    // Вручную набранное имя, не соответствующее записи в реестре (например, старое
    // "Permissions.Brands.View" вместо реального "Permissions.Catalog.Brands.View")
    // — это claim, который молча ничего не даёт.
    private static IReadOnlyList<DemoRole> BuildAcmeCustomRoles() =>
    [
        new(
            "Manager",
            "Operations manager — full catalog + tickets + read-only users.",
            [
                IdentityPermissions.Users.View,
                IdentityPermissions.Users.Update,
                IdentityPermissions.UserRoles.View,
                IdentityPermissions.Roles.View,
                IdentityPermissions.Sessions.View,
                IdentityPermissions.Sessions.Revoke,
                IdentityPermissions.Groups.View,
            ]),

        new(
            "Support",
            "Support agent — full tickets + read-only users.",
            [
                IdentityPermissions.Users.View,
                IdentityPermissions.UserRoles.View,
                IdentityPermissions.Sessions.View,
                IdentityPermissions.Sessions.Revoke,
            ]),
    ];
}
