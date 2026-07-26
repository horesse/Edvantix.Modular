using EDV.Framework.Shared.Identity.Authorization;
using EDV.Framework.Web.Idempotency;
using EDV.Modules.Billing.Contracts.Authorization;
using EDV.Modules.Billing.Contracts.Dtos;
using EDV.Modules.Billing.Contracts.v1.Usage;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Billing.Features.v1.Usage.CaptureUsageSnapshots;

public static class CaptureUsageSnapshotsEndpoint
{
    internal static RouteHandlerBuilder MapCaptureUsageSnapshotsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/usage/snapshots/capture",
                async (CaptureUsageSnapshotsCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CaptureUsageSnapshots")
            .WithSummary("Зафиксировать снимки использования для тенанта и периода вручную")
            .WithDescription("Эксплуатационный эндпоинт, оборачивающий IUsageReporter.CaptureForPeriodAsync. Идемпотентен: повторный запуск для той же пары (тенант, период) возвращает существующие снимки без изменений. Используется для ретроактивного выставления счетов, отладки и повторных запусков после исправлений.")
            .RequirePermission(BillingPermissions.Manage)
            .WithIdempotency()
            .Produces<IReadOnlyList<UsageSnapshotDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}
