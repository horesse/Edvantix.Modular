using EDV.Framework.Core.Common;
using EDV.Framework.Shared.Identity.Claims;
using EDV.Framework.Shared.Multitenancy;
using Finbuckle.MultiTenant.Abstractions;
using Hangfire.Client;
using Hangfire.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EDV.Framework.Jobs;

public class AppJobFilter : IClientFilter
{
    private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

    private readonly IServiceProvider _services;

    public AppJobFilter(IServiceProvider services) => _services = services;

    public void OnCreating(CreatingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Logger.InfoFormat("Установка параметров TenantId и UserId для задания {0}.{1}...",
            context.Job.Method.ReflectedType?.FullName, context.Job.Method.Name);

        using var scope = _services.CreateScope();

        var httpContextAccessor = scope.ServiceProvider.GetService<IHttpContextAccessor>();
        var httpContext = httpContextAccessor?.HttpContext;

        if (httpContext is null)
        {
            // Нет HTTP-контекста (например, создание периодического/фонового задания) – пропускаем установку арендатора/пользователя.
            Logger.WarnFormat("HttpContext недоступен для задания {0}.{1}; пропуск параметров арендатора/пользователя.",
                context.Job.Method.ReflectedType?.FullName, context.Job.Method.Name);
            return;
        }

        var mtAccessor = scope.ServiceProvider.GetService<IMultiTenantContextAccessor>();
        var tenantInfo = mtAccessor?.MultiTenantContext?.TenantInfo;
        if (tenantInfo is not null)
        {
            context.SetJobParameter(MultitenancyConstants.Identifier, tenantInfo);
        }

        var userId = httpContext.User.GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            context.SetJobParameter(QueryStringKeys.UserId, userId);
        }
    }

    public void OnCreated(CreatedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Logger.InfoFormat(
            "Задание создано с параметрами {0}",
            context.Parameters.Select(x => x.Key + "=" + x.Value).Aggregate((s1, s2) => s1 + ";" + s2));
    }
}