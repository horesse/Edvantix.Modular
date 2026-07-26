using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Contracts;
using EDV.Modules.Billing.Contracts.v1.Wallets;
using EDV.Modules.Billing.Data;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace EDV.Modules.Billing.Features.v1.Wallets.RejectTopupRequest;

public sealed class RejectTopupRequestCommandHandler(
    BillingDbContext db,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : ICommandHandler<RejectTopupRequestCommand, Guid>
{
    public async ValueTask<Guid> Handle(RejectTopupRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var callerTenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Требуется контекст тенанта.");
        var isRoot = callerTenantId == MultitenancyConstants.Root.Id;

        var request = await db.TopupRequests
            .FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Запрос на пополнение {command.Id} не найден.");

        if (!isRoot && request.TenantId != callerTenantId)
        {
            throw new UnauthorizedException("Вы можете отклонять запросы на пополнение только для своего тенанта.");
        }

        if (request.Status != TopupRequestStatus.Pending)
        {
            throw new CustomException(
                $"Запрос на пополнение {command.Id} не может быть отклонён, так как находится в статусе {request.Status} (отклонить можно только запросы в статусе Pending).",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        request.Reject(command.Reason);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Id;
    }
}
