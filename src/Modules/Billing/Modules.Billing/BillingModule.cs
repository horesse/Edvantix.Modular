using Asp.Versioning;
using EDV.Framework.Eventing;
using EDV.Framework.Persistence;
using EDV.Framework.Shared.Identity;
using EDV.Framework.Web.Modules;
using EDV.Modules.Billing;
using EDV.Modules.Billing.Contracts.Authorization;
using EDV.Modules.Billing.Data;
using EDV.Modules.Billing.Features.v1.Invoices.GenerateInvoices;
using EDV.Modules.Billing.Features.v1.Invoices.GetInvoiceById;
using EDV.Modules.Billing.Features.v1.Invoices.GetInvoicePdf;
using EDV.Modules.Billing.Features.v1.Invoices.GetInvoices;
using EDV.Modules.Billing.Features.v1.Invoices.GetMyInvoices;
using EDV.Modules.Billing.Features.v1.Invoices.IssueInvoice;
using EDV.Modules.Billing.Features.v1.Invoices.MarkInvoicePaid;
using EDV.Modules.Billing.Features.v1.Invoices.VoidInvoice;
using EDV.Modules.Billing.Features.v1.Plans.CreatePlan;
using EDV.Modules.Billing.Features.v1.Plans.GetPlans;
using EDV.Modules.Billing.Features.v1.Plans.UpdatePlan;
using EDV.Modules.Billing.Features.v1.Subscriptions.AssignSubscription;
using EDV.Modules.Billing.Features.v1.Subscriptions.GetSubscription;
using EDV.Modules.Billing.Features.v1.Usage.CaptureUsageSnapshots;
using EDV.Modules.Billing.Features.v1.Usage.GetUsageSnapshots;
using EDV.Modules.Billing.Features.v1.Wallets.ApproveTopupRequest;
using EDV.Modules.Billing.Features.v1.Wallets.CreateTopupRequest;
using EDV.Modules.Billing.Features.v1.Wallets.GetMyTopupRequests;
using EDV.Modules.Billing.Features.v1.Wallets.GetMyWallet;
using EDV.Modules.Billing.Features.v1.Wallets.GetTopupRequests;
using EDV.Modules.Billing.Features.v1.Wallets.RejectTopupRequest;
using EDV.Modules.Billing.Services;
using Hangfire;
using Hangfire.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

[assembly: Module(typeof(BillingModule), 500)]

namespace EDV.Modules.Billing;

public sealed class BillingModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(
            BillingPermissions.All);

        builder.Services.AddDbContext<BillingDbContext>();
        builder.Services.AddScoped<IDbInitializer, BillingDbInitializer>();
        builder.Services.AddScoped<IUsageReporter, UsageReporter>();
        builder.Services.AddScoped<IBillingService, BillingService>();
        builder.Services.AddSingleton<IInvoicePdfRenderer, InvoicePdfRenderer>();

        // Реагирует на события создания/продления тенанта (Multitenancy.Contracts), управляя подписками и счетами.
        builder.Services.AddIntegrationEventHandlers(typeof(BillingModule).Assembly);

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<BillingDbContext>(
                name: "db:billing",
                failureStatus: HealthStatus.Unhealthy);
    }

    public void ConfigureMiddleware(IApplicationBuilder app)
    {
        // Дополнительное промежуточное ПО не требуется
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var versionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints
            .MapGroup("api/v{version:apiVersion}/billing")
            .WithTags("Billing")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapGetPlansEndpoint();
        group.MapCreatePlanEndpoint();
        group.MapUpdatePlanEndpoint();

        group.MapGetSubscriptionEndpoint();
        group.MapGetMySubscriptionEndpoint();
        group.MapAssignSubscriptionEndpoint();

        group.MapGetInvoicesEndpoint();
        group.MapGetMyInvoicesEndpoint();
        group.MapGetInvoiceByIdEndpoint();
        group.MapGetInvoicePdfEndpoint();
        group.MapGenerateInvoicesEndpoint();
        group.MapIssueInvoiceEndpoint();
        group.MapMarkInvoicePaidEndpoint();
        group.MapVoidInvoiceEndpoint();

        group.MapGetUsageSnapshotsEndpoint();
        group.MapCaptureUsageSnapshotsEndpoint();

        group.MapGetMyWalletEndpoint();
        group.MapCreateTopupRequestEndpoint();
        group.MapGetMyTopupRequestsEndpoint();

        group.MapGetTopupRequestsEndpoint();
        group.MapApproveTopupRequestEndpoint();
        group.MapRejectTopupRequestEndpoint();

        var jobManager = endpoints.ServiceProvider.GetService<IRecurringJobManager>();
        if (jobManager is not null)
        {
            // Запускается в 00:05 UTC 1-го числа каждого месяца; задача выставляет счета за предыдущий период.
            jobManager.AddOrUpdate(
                "billing-monthly-invoices",
                Job.FromExpression<MonthlyInvoiceJob>(j => j.RunAsync(CancellationToken.None)),
                "5 0 1 * *",
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
    }
}
