using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Contracts.v1.Wallets;
using EDV.Modules.Billing.Data;
using EDV.Modules.Billing.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;

namespace EDV.Modules.Billing.Features.v1.Wallets.CreateTopupRequest;

public sealed class CreateTopupRequestCommandHandler(
    BillingDbContext db,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
    ICurrentUser currentUser)
    : ICommandHandler<CreateTopupRequestCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateTopupRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // BillingDbContext не фильтруется по тенанту; определяем собственный тенант вызывающего и строго ограничиваем область им.
        var tenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Требуется контекст тенанта.");

        var requestedBy = currentUser.IsAuthenticated() ? currentUser.GetUserId().ToString() : null;
        var request = TopupRequest.Create(tenantId, command.Amount, "USD", command.Note, requestedBy);
        db.TopupRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Id;
    }
}
