using EDV.Framework.Eventing.Abstractions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Billing.Contracts.v1.Plans;
using EDV.Modules.Multitenancy.Contracts;
using EDV.Modules.Multitenancy.Contracts.Events;
using EDV.Modules.Multitenancy.Contracts.v1.CreateTenant;
using EDV.Modules.Multitenancy.Provisioning;
using Mediator;
using Microsoft.Extensions.Options;

namespace EDV.Modules.Multitenancy.Features.v1.CreateTenant;

public sealed class CreateTenantCommandHandler(
    ITenantService tenantService,
    ITenantProvisioningService provisioningService,
    ITenantInitialPasswordBuffer passwordBuffer,
    IMediator mediator,
    IEventBus events,
    IOptions<TenantBillingOptions> billingOptions,
    TimeProvider timeProvider)
    : ICommandHandler<CreateTenantCommand, CreateTenantCommandResponse>
{
    public async ValueTask<CreateTenantCommandResponse> Handle(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Определяем план (по умолчанию — пробный) и читаем его срок, чтобы задать окно действия
        // тенанта. Некорректный ключ плана приводит к исключению NotFound (400) ещё до создания тенанта.
        var planKey = string.IsNullOrWhiteSpace(command.PlanKey)
            ? billingOptions.Value.DefaultPlanKey
            : command.PlanKey!;
        var term = await mediator.Send(new GetPlanTermQuery(planKey), cancellationToken).ConfigureAwait(false);

        var periodStart = timeProvider.GetUtcNow().UtcDateTime;
        var periodEnd = periodStart.AddMonths(term.TermMonths);

        var tenantId = await tenantService.CreateAsync(
            command.Id,
            command.Name,
            command.ConnectionString,
            command.AdminEmail,
            command.Issuer,
            term.Key,
            periodEnd,
            cancellationToken).ConfigureAwait(false);

        // Буферизуем пароль администратора для фонового шага заполнения данными в IdentityDbInitializer,
        // сохраняя его до вызова StartAsync, чтобы заполнение никогда не выполнилось раньше буферизации.
        passwordBuffer.Store(tenantId, command.AdminPassword);

        var provisioning = await provisioningService.StartAsync(tenantId, cancellationToken).ConfigureAwait(false);

        // Запускаем побочные эффекты биллинга (подписка + счёт за период) через интеграционное событие,
        // чтобы модуль Multitenancy оставался независимым от рантайма Billing.
        await events.PublishAsync(new TenantSubscribedIntegrationEvent(
            Id: Guid.NewGuid(),
            OccurredOnUtc: periodStart,
            TenantId: tenantId,
            CorrelationId: provisioning.CorrelationId,
            Source: "Multitenancy",
            PlanId: term.PlanId,
            PlanKey: term.Key,
            PeriodStartUtc: periodStart,
            PeriodEndUtc: periodEnd), cancellationToken).ConfigureAwait(false);

        return new CreateTenantCommandResponse(
            tenantId,
            provisioning.CorrelationId,
            provisioning.Status.ToString());
    }
}
