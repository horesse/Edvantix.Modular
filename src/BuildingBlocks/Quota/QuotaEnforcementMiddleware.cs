using EDV.Framework.Shared.Auditing;
using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Shared.Quota;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;

namespace EDV.Framework.Quota;

/// <summary>
/// Проверка квот на каждый запрос для <see cref="QuotaResource.ApiCalls"/>. Выполняется после аутентификации и
/// ограничителя скорости, чтобы только аутентифицированные и учтённые вызовы расходовали квоту.
/// Пропускает проверки здоровья, корневого арендатора и неразрешённых арендаторов. Отклоняет с HTTP 429 + RFC 9457 ProblemDetails
/// и заголовком <c>Retry-After</c>; помечает запрос через <see cref="HttpContextItemKeys.QuotaRejected"/>,
/// чтобы промежуточное ПО аудита могло пометить событие активности тегом <c>AuditTag.OutOfQuota</c> без
/// создания зависимости этого промежуточного ПО от модуля аудита.
/// </summary>
public sealed class QuotaEnforcementMiddleware : IMiddleware
{
    private const string QuotaExceededType = "https://datatracker.ietf.org/doc/html/rfc6585#section-4";

    private readonly IQuotaService _quotaService;
    private readonly IMultiTenantContextAccessor<AppTenantInfo> _tenantAccessor;
    private readonly QuotaOptions _options;
    private readonly ILogger<QuotaEnforcementMiddleware> _logger;
    private readonly TimeProvider _timeProvider;

    public QuotaEnforcementMiddleware(
        IQuotaService quotaService,
        IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
        QuotaOptions options,
        ILogger<QuotaEnforcementMiddleware> logger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(quotaService);
        ArgumentNullException.ThrowIfNull(tenantAccessor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _quotaService = quotaService;
        _tenantAccessor = tenantAccessor;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (!_options.Enabled || IsExempt(context))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var tenantId = _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var result = await _quotaService
            .CheckAndRecordAsync(tenantId, QuotaResource.ApiCalls, 1, context.RequestAborted)
            .ConfigureAwait(false);

        if (result.Allowed)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        await RejectAsync(context, tenantId, result).ConfigureAwait(false);
    }

    private async Task RejectAsync(HttpContext context, string tenantId, QuotaCheckResult result)
    {
        context.Items[HttpContextItemKeys.QuotaRejected] = true;

        var retryAfterSeconds = CalculateRetryAfter(result.ResetAtUtc);
        if (retryAfterSeconds is > 0)
        {
            context.Response.Headers.RetryAfter = retryAfterSeconds.Value
                .ToString(CultureInfo.InvariantCulture);
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Квота превышена",
            Detail = $"Месячная квота для {result.Resource} достигнута ({result.CurrentUsage}/{result.Limit}).",
            Type = QuotaExceededType,
            Instance = context.Request.Path
        };

        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        problem.Extensions["traceId"] = traceId;

        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? context.TraceIdentifier;
        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["resource"] = result.Resource.ToString();
        problem.Extensions["limit"] = result.Limit;
        problem.Extensions["currentUsage"] = result.CurrentUsage;
        if (result.ResetAtUtc is not null)
        {
            problem.Extensions["resetAtUtc"] = result.ResetAtUtc.Value.ToString("o", CultureInfo.InvariantCulture);
        }

        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(
                "Отклонён запрос для арендатора {TenantId} — превышена квота {Resource} ({Current}/{Limit})",
                tenantId, result.Resource, result.CurrentUsage, result.Limit);
        }

        await context.Response.WriteAsJsonAsync(problem, context.RequestAborted).ConfigureAwait(false);
    }

    private int? CalculateRetryAfter(DateTimeOffset? resetAtUtc)
    {
        if (resetAtUtc is null)
        {
            return null;
        }

        var delta = resetAtUtc.Value - _timeProvider.GetUtcNow();
        return delta.TotalSeconds > 0 ? (int)Math.Ceiling(delta.TotalSeconds) : null;
    }

    private static bool IsExempt(HttpContext context)
    {
        var path = context.Request.Path;
        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/ready", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/live", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/alive", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase);
    }
}