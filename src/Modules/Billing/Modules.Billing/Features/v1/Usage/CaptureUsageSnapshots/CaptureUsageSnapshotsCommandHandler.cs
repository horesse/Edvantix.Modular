using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Contracts.Dtos;
using EDV.Modules.Billing.Contracts.v1.Usage;
using EDV.Modules.Billing.Services;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;

namespace EDV.Modules.Billing.Features.v1.Usage.CaptureUsageSnapshots;

public sealed class CaptureUsageSnapshotsCommandHandler(
    IUsageReporter reporter,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : ICommandHandler<CaptureUsageSnapshotsCommand, IReadOnlyList<UsageSnapshotDto>>
{
    public async ValueTask<IReadOnlyList<UsageSnapshotDto>> Handle(
        CaptureUsageSnapshotsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Только root-оператор может зафиксировать использование для произвольного тенанта; вызывающий
        // в контексте тенанта ограничен своим тенантом и не может подделать снимки использования/перерасхода
        // другого тенанта.
        var callerTenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Требуется контекст тенанта.");
        var isRoot = callerTenantId == MultitenancyConstants.Root.Id;
        var targetTenantId = isRoot ? command.TenantId : callerTenantId;

        var snapshots = await reporter
            .CaptureForPeriodAsync(targetTenantId, command.PeriodYear, command.PeriodMonth, cancellationToken)
            .ConfigureAwait(false);

        return snapshots
            .Select(s => new UsageSnapshotDto(
                s.Id,
                s.TenantId,
                s.PeriodYear,
                s.PeriodMonth,
                s.Resource,
                s.UsedUnits,
                s.LimitUnits,
                s.Overage,
                s.CapturedAtUtc))
            .ToList();
    }
}
