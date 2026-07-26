using EDV.Framework.Persistence.Specifications;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Multitenancy.Contracts.Dtos;
using EDV.Modules.Multitenancy.Contracts.v1.GetTenants;
using System.Linq.Expressions;

namespace EDV.Modules.Multitenancy.Features.v1.GetTenants;

internal sealed class GetTenantsSpecification : Specification<AppTenantInfo, TenantDto>
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<AppTenantInfo, object>>> SortMappings =
        new Dictionary<string, Expression<Func<AppTenantInfo, object>>>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = t => t.Id!,
            ["name"] = t => t.Name!,
            ["connectionstring"] = t => t.ConnectionString!,
            ["adminemail"] = t => t.AdminEmail!,
            ["isactive"] = t => t.IsActive,
            ["validupto"] = t => t.ValidUpto,
            ["issuer"] = t => t.Issuer!
        };

    public GetTenantsSpecification(GetTenantsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Проекция по умолчанию в TenantDto.
        Select(t => new TenantDto
        {
            Id = t.Id!,
            Name = t.Name!,
            ConnectionString = t.ConnectionString,
            AdminEmail = t.AdminEmail!,
            IsActive = t.IsActive,
            ValidUpto = t.ValidUpto,
            Issuer = t.Issuer
        });

        // Поведение по умолчанию: без отслеживания изменений (no tracking).
        AsNoTrackingQuery();

        ApplySortingOverride(
            query.Sort,
            () =>
            {
                OrderBy(t => t.Name!);
                ThenBy(t => t.Id!);
            },
            SortMappings);
    }
}