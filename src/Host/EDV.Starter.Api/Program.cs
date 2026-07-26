using EDV.Framework.Web;
using EDV.Framework.Web.Modules;
using EDV.Modules.Auditing;
using EDV.Modules.Billing;
using EDV.Modules.Identity;
using EDV.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using EDV.Modules.Identity.Features.v1.Tokens.TokenGeneration;
using EDV.Modules.Multitenancy;
using EDV.Modules.Multitenancy.Contracts.v1.GetTenantStatus;
using EDV.Modules.Multitenancy.Features.v1.GetTenantStatus;
using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Сериализуем перечисления как строковые имена (чтение по-прежнему принимает имена или целые числа).
// [Flags]-перечисления (AuditTag, BodyCapture) используют собственный NumericEnumConverter,
// так как строки с объединением через запятую нарушают битовые потребители. Фронтенды зеркалируют это как строковые объединения.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

if (builder.Environment.IsProduction())
{
    static void Require(IConfiguration config, string key)
    {
        if (string.IsNullOrWhiteSpace(config[key]))
        {
            throw new InvalidOperationException($"Отсутствует обязательная конфигурация '{key}' в Production.");
        }
    }

    var config = builder.Configuration;
    Require(config, "DatabaseOptions:ConnectionString");
    Require(config, "CachingOptions:Redis");
    Require(config, "JwtOptions:SigningKey");
}

builder.Services.AddMediator(o =>
{
    o.ServiceLifetime = ServiceLifetime.Scoped;
    o.Assemblies = [
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

builder.AddPlatform(o =>
{
    o.EnableCaching = true;
    o.EnableMailing = true;
    o.EnableJobs = true;
    o.EnableQuotas = true;
    o.EnableSse = true;
    o.EnableRealtime = true;
});

builder.AddModules(moduleAssemblies);

// Самовосстановление развёртываний с устаревшими периодическими заданиями Hangfire для каждого модуля `{module}-outbox-dispatcher`
// (outbox теперь отправляется через OutboxDispatcherHostedService). Ничего не делает после очистки хранилища.
builder.Services.AddHostedService<EDV.Starter.Api.OrphanedOutboxRecurringJobCleanupService>();

// Демо-данные подготавливаются глаголом `seed-demo` DbMigrator, а не API — API никогда не изменяет данные при запуске.
// См. src/Host/EDV.Starter.DbMigrator/README.md.

var app = builder.Build();

app.UseMultiTenantDatabases();
app.UsePlatform(p =>
{
    p.MapModules = true;
    p.ServeStaticFiles = true;
    p.UseQuotas = true;
    p.MapSseEndpoints = true;
    p.MapRealtime = true;
});

app.MapGet("/", () => Results.Ok(new { message = "hello world!" }))
   .WithTags("PlayGround")
   .AllowAnonymous();
await app.RunAsync();