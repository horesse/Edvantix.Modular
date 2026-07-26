using EDV.Modules.Multitenancy.Contracts.Dtos;
using Mediator;

namespace EDV.Modules.Multitenancy.Contracts.v1.GetTenantMigrations;

public sealed record GetTenantMigrationsQuery : IQuery<IReadOnlyCollection<TenantMigrationStatusDto>>;