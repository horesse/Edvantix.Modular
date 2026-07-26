using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Web.Modules;
using EDV.Starter.DbMigrator;
using EDV.Starter.DbMigrator.DemoSeed;
using System.Globalization;
using System.Reflection;
using EDV.Framework.Web;
using EDV.Modules.Auditing;
using EDV.Modules.Billing;
using EDV.Modules.Identity;
using EDV.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using EDV.Modules.Identity.Features.v1.Tokens.TokenGeneration;
using EDV.Modules.Multitenancy;
using EDV.Modules.Multitenancy.Contracts;
using EDV.Modules.Multitenancy.Contracts.v1.GetTenantStatus;
using EDV.Modules.Multitenancy.Data;
using EDV.Modules.Multitenancy.Features.v1.GetTenantStatus;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// EDV DbMigrator — однократная консольная утилита, которая мигрирует все БД до актуальной версии,
// опционально заполняет начальными данными, затем завершается с кодом 0/1.
// Запускается как этап развёртывания (не при запуске API) и может использовать строку подключения с повышенными привилегиями DDL.
// Глаголы: см. MigratorCommand.HelpText.

var cli = MigratorCommand.Parse(args);
if (cli.Help)
{
    await Console.Out.WriteLineAsync(MigratorCommand.HelpText).ConfigureAwait(false);
    return 0;
}
var builder = Host.CreateApplicationBuilder(args);

// Отключаем валидацию DI во время сборки: автоматически включена в Development,
// она обходит ВСЕ дескрипторы, включая обработчики, которые этот сокращённый процесс
// никогда не вызывает (Chat→IHubContext, Identity→IMailService) и выдаёт ложные срабатывания.
builder.ConfigureContainer(new DefaultServiceProviderFactory(
    new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = false }));

// При dotnet run текущая папка — это папка проекта, но appsettings.json копируется в выходную папку,
// поэтому загружаем его из AppContext.BaseDirectory для валидации JwtOptions модуля Identity.
builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true);
builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true);

// Повторно добавляем переменные окружения и аргументы командной строки, чтобы они сохраняли приоритет над добавленными вручную JSON-файлами.
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

// JwtOptions.ValidateOnStart() модуля Identity падает на пустом SigningKey в базовом appsettings, но
// мигратор никогда не создаёт JWT. Внедряем заполнитель с пометкой только когда ничего реального не настроено.
if (string.IsNullOrWhiteSpace(builder.Configuration["JwtOptions:SigningKey"]))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["JwtOptions:SigningKey"] = "edv-dbmigrator-placeholder-never-mints-tokens-32+",
        ["JwtOptions:Issuer"] = builder.Configuration["JwtOptions:Issuer"] ?? "edv.local",
        ["JwtOptions:Audience"] = builder.Configuration["JwtOptions:Audience"] ?? "edv.clients",
    });
}

// Быстрый отказ с одной понятной строкой, если DatabaseOptions__ConnectionString не задан,
// вместо того, чтобы позволить валидации опций во время сборки хоста выбрасывать стек-трейс.
if (string.IsNullOrWhiteSpace(builder.Configuration["DatabaseOptions:ConnectionString"]))
{
    await Console.Error.WriteLineAsync(
        "[migrator] ОШИБКА: DatabaseOptions:ConnectionString пуста — отказ от запуска без настроенной целевой базы. "
        + "Установите DatabaseOptions__ConnectionString в строку подключения с повышенными привилегиями DDL перед вызовом мигратора.")
        .ConfigureAwait(false);
    return 1;
}

// Зеркалируем регистрацию Mediator из API, чтобы обработчики модулей подключались корректно —
// некоторые DbInitializers модулей зависят от сервисов, которые строятся конвейерами Mediator.
builder.Services.AddMediator(o =>
{
    o.ServiceLifetime = ServiceLifetime.Scoped;
    o.Assemblies =
    [
        typeof(GenerateTokenCommand),
        typeof(GenerateTokenCommandHandler),
        typeof(GetTenantStatusQuery),
        typeof(GetTenantStatusQueryHandler),
        typeof(EDV.Modules.Auditing.Contracts.AuditEnvelope),
        typeof(EDV.Modules.Auditing.Persistence.AuditDbContext),
        typeof(EDV.Modules.Billing.Contracts.BillingContractsMarker),
        typeof(BillingModule),
    ];
});

var moduleAssemblies = new Assembly[]
{
    typeof(IdentityModule).Assembly,
    typeof(MultitenancyModule).Assembly,
    typeof(AuditingModule).Assembly,
    typeof(BillingModule).Assembly,
};

// Отключаем runtime-специфичные компоненты; сохраняем только сохраняемость + мультиарендность, чтобы DbInitializers разрешались.
// Кэширование оставляем, потому что конструкторы некоторых модулей обращаются к IDistributedCache (in-memory fallback, если нет Redis).
builder.AddPlatform(o =>
{
    o.EnableOpenTelemetry = false;
    o.EnableCors = false;
    o.EnableOpenApi = false;
    o.EnableJobs = false;
    o.EnableMailing = false;
    o.EnableSse = false;
    o.EnableRealtime = false;
    o.EnableQuotas = false;
    o.EnableFeatureFlags = false;
    o.EnableIdempotency = false;
    o.EnableCaching = true;
});

builder.AddModules(moduleAssemblies);

// TenantProvisioningService требуется IJobService, но Hangfire отключён через EnableJobs (здесь выключен).
// Предоставляем заглушку, которая выбрасывает исключение, чтобы граф DI разрешался; код миграции не ставит задания в очередь.
builder.Services.AddSingleton<EDV.Framework.Jobs.Services.IJobService, NoOpJobService>();

// Удаляем все BackgroundService (+ TenantStoreInitializerHostedService) перед StartAsync: если они останутся,
// они будут опрашивать/записывать таблицы ДО того, как Шаг 1/2 их создадут (42P01).
// StartupValidator и Serilog flush IHostedServices остаются.
foreach (var descriptor in builder.Services
    .Where(d => d.ServiceType == typeof(IHostedService)
        && (typeof(BackgroundService).IsAssignableFrom(d.ImplementationType)
            || d.ImplementationType?.Name == "TenantStoreInitializerHostedService"))
    .ToList())
{
    builder.Services.Remove(descriptor);
}

// DemoSeeder включается через глагол `seed-demo`. Регистрируем безусловно, чтобы
// граф DI был удовлетворён; диспетчеризация по глаголу ниже решает, вызывать ли его.
builder.Services.AddScoped<DemoSeeder>();

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<MigratorCommand>>();

// Запускаем хост, чтобы инициализировались провайдеры логирования / валидаторы опций.
await host.StartAsync().ConfigureAwait(false);

try
{
    // ── Шаг 0 — ожидание доступности базы данных ────────────────────────
    // Postgres может ещё инициализироваться при холодном старте; экспоненциальная задержка (≤2 мин),
    // затем TimeoutException + выход 1.
    var connectionString = host.Services.GetRequiredService<IConfiguration>()["DatabaseOptions:ConnectionString"]
        ?? throw new InvalidOperationException("DatabaseOptions:ConnectionString не настроена.");
    await Console.Out.WriteLineAsync("[migrator] ожидание postgres…").ConfigureAwait(false);
    await PostgresMigratorLock.WaitForDatabaseAsync(connectionString, logger, CancellationToken.None)
        .ConfigureAwait(false);
    await Console.Out.WriteLineAsync("[migrator] postgres готов").ConfigureAwait(false);

    // Логируем роль + базу данных подключения, чтобы неправильно настроенная строка с низкими привилегиями
    // проявилась сейчас, а не как "permission denied for schema public" во время MigrateAsync.
    await LogConnectionIdentityAsync(connectionString).ConfigureAwait(false);

    // ── Шаг 0b — захват консультативной блокировки ──────────────────────
    // Блокировка уровня сессии: конкурентные запуски блокируются здесь;
    // автоматически снимается при закрытии соединения (без сирот при аварийном завершении).
    await Console.Out.WriteLineAsync("[migrator] захват консультативной блокировки…").ConfigureAwait(false);
    await using var migratorLock = await PostgresMigratorLock
        .AcquireAsync(connectionString, logger, CancellationToken.None)
        .ConfigureAwait(false);
    await Console.Out.WriteLineAsync("[migrator] консультативная блокировка захвачена").ConfigureAwait(false);

    // ── Шаг 1 — каталог арендаторов ──────────────────────────────────────
    // Всегда применяется первым: мигратор для каждого арендатора ниже считывает всех арендаторов из этой базы.
    using (var scope = host.Services.CreateScope())
    {
        var tenantDb = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        var pending = (await tenantDb.Database.GetPendingMigrationsAsync(CancellationToken.None)
            .ConfigureAwait(false)).ToList();

        if (cli.Command == "list-pending")
        {
            await Console.Out.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"[tenant-catalog] {pending.Count} ожидающих миграций"))
                .ConfigureAwait(false);
            foreach (var name in pending)
            {
                await Console.Out.WriteLineAsync($"  · {name}").ConfigureAwait(false);
            }
        }
        else if (pending.Count > 0)
        {
            await Console.Out.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"[tenant-catalog] применение {pending.Count} миграций…"))
                .ConfigureAwait(false);
            await tenantDb.Database.MigrateAsync(CancellationToken.None).ConfigureAwait(false);
            await Console.Out.WriteLineAsync("[tenant-catalog] готово").ConfigureAwait(false);
        }
        else
        {
            await Console.Out.WriteLineAsync("[tenant-catalog] уже актуально").ConfigureAwait(false);
        }

        // Заполняем корневого арендатора при первом запуске каталога, чтобы
        // проход по арендаторам ниже имел хотя бы одного арендатора для обхода.
        var seeded = await tenantDb.TenantInfo
            .FindAsync([MultitenancyConstants.Root.Id], CancellationToken.None)
            .ConfigureAwait(false);
        if (seeded is null && cli.Command != "list-pending")
        {
            var rootTenant = new AppTenantInfo(
                MultitenancyConstants.Root.Id,
                MultitenancyConstants.Root.Name,
                connectionString: string.Empty,
                MultitenancyConstants.Root.EmailAddress,
                issuer: MultitenancyConstants.Root.Issuer);
            rootTenant.SetValidity(TimeProvider.System.GetUtcNow().UtcDateTime.AddYears(1));
            await tenantDb.TenantInfo.AddAsync(rootTenant, CancellationToken.None).ConfigureAwait(false);
            await tenantDb.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            await Console.Out.WriteLineAsync("[tenant-catalog] создан корневой арендатор").ConfigureAwait(false);
        }
    }

    // ── Шаг 2 — миграции для каждого арендатора + (опционально) заполнение ──
    // `seed-demo` обходит этот шаг: он создаёт свои демо-арендаторы inline (Шаг 3 ниже).
    if (!cli.CatalogOnly && cli.Command != "seed-demo")
    {
        var tenantStore = host.Services.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var tenantService = host.Services.GetRequiredService<ITenantService>();

        var allTenants = (await tenantStore.GetAllAsync().ConfigureAwait(false)).ToList();
        var tenants = string.IsNullOrEmpty(cli.Tenant)
            ? allTenants
            : allTenants.Where(t => string.Equals(t.Id, cli.Tenant, StringComparison.OrdinalIgnoreCase)).ToList();

        if (tenants.Count == 0)
        {
            await Console.Out.WriteLineAsync($"[migrator] не найдено арендаторов, соответствующих {cli.Tenant ?? "(все)"}")
                .ConfigureAwait(false);
        }

        foreach (var tenant in tenants)
        {
            if (cli.Command == "list-pending")
            {
                await Console.Out.WriteLineAsync(
                    $"[{tenant.Id}] миграции оцениваются для каждого арендатора IDbInitializer каждого модуля")
                    .ConfigureAwait(false);
                continue;
            }
            if (cli.Command == "seed")
            {
                await Console.Out.WriteLineAsync($"[{tenant.Id}] заполнение…").ConfigureAwait(false);
                await tenantService.SeedTenantAsync(tenant, CancellationToken.None).ConfigureAwait(false);
                continue;
            }

            await Console.Out.WriteLineAsync($"[{tenant.Id}] миграция…").ConfigureAwait(false);
            await tenantService.MigrateTenantAsync(tenant, CancellationToken.None).ConfigureAwait(false);

            if (cli.SeedAfter)
            {
                await Console.Out.WriteLineAsync($"[{tenant.Id}] заполнение…").ConfigureAwait(false);
                await tenantService.SeedTenantAsync(tenant, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    // ── Шаг 3 — демо-заполнение (глагол: `seed-demo`) ─────────────────────
    // Только для разработки: создаёт acme + globex с насыщенным демо-контентом; завершается с ошибкой вне Development.
    if (cli.Command == "seed-demo")
    {
        var env = host.Services.GetRequiredService<IHostEnvironment>();
        if (!env.IsDevelopment())
        {
            await Console.Error.WriteLineAsync(
                $"[demo-seed] ОТКАЗ ОТ ВЫПОЛНЕНИЯ — DOTNET_ENVIRONMENT = '{env.EnvironmentName}'. "
                + "seed-demo предназначен только для разработки.")
                .ConfigureAwait(false);
            return 1;
        }

        await Console.Out.WriteLineAsync("[demo-seed] подготовка acme + globex с демо-контентом…")
            .ConfigureAwait(false);
        using var scope = host.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
        await seeder.RunAsync(CancellationToken.None).ConfigureAwait(false);
        await Console.Out.WriteLineAsync("[demo-seed] готово").ConfigureAwait(false);
    }

    await Console.Out.WriteLineAsync("[migrator] успешно завершён.").ConfigureAwait(false);
    return 0;
}
#pragma warning disable CA1031 // Top-level Main намеренно перехватывает все исключения для преобразования любой ошибки в код выхода 1.
catch (Exception ex)
#pragma warning restore CA1031
{
    logger.LogError(ex, "DbMigrator не удался");
    await Console.Error.WriteLineAsync($"[migrator] ОШИБКА: {ex.GetType().Name}: {ex.Message}")
        .ConfigureAwait(false);
    if (ex.StackTrace is { } stack)
    {
        await Console.Error.WriteLineAsync(stack).ConfigureAwait(false);
    }
    return 1;
}
finally
{
    // Сбрасываем буферы логирования + выполняем остановку хоста, чтобы оператор
    // (и сборщик логов CI) видели последние строки перед выходом процесса.
    await host.StopAsync().ConfigureAwait(false);
}

static async Task LogConnectionIdentityAsync(string connectionString)
{
    // Зондирование идентификации с максимальными усилиями — никогда не прерываем мигратор из-за шага логирования.
    try
    {
        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT current_user, current_database()";
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            var role = reader.GetString(0);
            var db = reader.GetString(1);
            await Console.Out.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"[migrator] подключён как role={role} database={db}")).ConfigureAwait(false);
        }
    }
#pragma warning disable CA1031 // Путь только для логирования: любое исключение проглатывается и сообщается, но не является фатальным.
    catch (Exception ex)
#pragma warning restore CA1031
    {
        await Console.Out.WriteLineAsync($"[migrator] ПРЕДУПРЕЖДЕНИЕ: не удалось залогировать идентификацию подключения: {ex.Message}")
            .ConfigureAwait(false);
    }
}