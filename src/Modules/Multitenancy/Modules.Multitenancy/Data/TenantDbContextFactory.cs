using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EDV.Modules.Multitenancy.Data;

public sealed class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        // Фабрика для design-time: читает конфигурацию (appsettings + переменные окружения), чтобы определить провайдер и подключение.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var provider = configuration["DatabaseOptions:Provider"] ?? "POSTGRESQL";
        var connectionString = configuration["DatabaseOptions:ConnectionString"]
            ?? "Host=localhost;Database=edv-tenant;Username=postgres;Password=postgres";
        var migrationsAssembly = configuration["DatabaseOptions:MigrationsAssembly"]
            ?? "EDV.Starter.Migrations.PostgreSQL";
        var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();

        switch (provider.ToUpperInvariant())
        {
            case "POSTGRESQL":
                optionsBuilder.UseNpgsql(
                    connectionString,
                    b => b.MigrationsAssembly(migrationsAssembly));
                break;
            default:
                throw new NotSupportedException($"Провайдер базы данных '{provider}' не поддерживается для миграций TenantDbContext.");
        }

        return new TenantDbContext(optionsBuilder.Options);
    }
}