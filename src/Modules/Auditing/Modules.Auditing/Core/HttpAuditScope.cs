using EDV.Framework.Core.Context;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Auditing.Contracts;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Security.Claims;

namespace EDV.Modules.Auditing.Core;

/// <summary>
/// Область аудита, учитывающая окружающий контекст. Предпочитает HTTP-контекст,
/// когда он есть (обычный путь), и откатывается на Finbuckle-аксессор арендатора
/// и <see cref="ICurrentUser"/> для не-HTTP выполнения (задания Hangfire, фоновые
/// воркеры). Именно запасной путь атрибутирует аудит изменения сущностей,
/// захваченный <c>AuditingSaveChangesInterceptor</c>, когда EF Core делает flush
/// из области сервиса, управляемой Hangfire.
///
/// Имя класса сохранено для совместимости с существующими регистрациями DI;
/// поведение теперь шире, чем предполагает название.
/// </summary>
public sealed class HttpAuditScope : IAuditScope
{
    private readonly IHttpContextAccessor _http;
    private readonly IMultiTenantContextAccessor<AppTenantInfo> _tenant;
    private readonly ICurrentUser? _currentUser;

    public HttpAuditScope(
        IHttpContextAccessor httpContextAccessor,
        IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
        ICurrentUser? currentUser = null)
    {
        _http = httpContextAccessor;
        _tenant = tenantAccessor;
        _currentUser = currentUser;
    }

    public string? TenantId =>
        _tenant.MultiTenantContext?.TenantInfo?.Id
        ?? _http.HttpContext?.User?.FindFirstValue(MultitenancyConstants.Identifier)
        ?? _http.HttpContext?.Request?.Headers[MultitenancyConstants.Identifier].FirstOrDefault()
        ?? _http.HttpContext?.Items["TenantId"] as string
        ?? _currentUser?.GetTenant();

    public string? UserId =>
        _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _http.HttpContext?.User?.FindFirstValue("sub")
        ?? NullIfEmpty(_currentUser?.GetUserId().ToString());

    public string? UserName =>
        _http.HttpContext?.User?.Identity?.Name
        ?? _http.HttpContext?.User?.FindFirstValue("name")
        ?? _currentUser?.Name;

    // Activity.Current заполняется как ASP.NET Core (HTTP), так и
    // AppJobActivator (Hangfire), поэтому это работает в обоих контекстах.
    public string? TraceId => Activity.Current?.TraceId.ToString();
    public string? SpanId => Activity.Current?.SpanId.ToString();

    public string? CorrelationId =>
        _http.HttpContext?.TraceIdentifier
        ?? Activity.Current?.RootId;

    public string? RequestId =>
        _http.HttpContext?.TraceIdentifier
        ?? Activity.Current?.Id;

    public string? Source =>
        _http.HttpContext?.GetEndpoint()?.DisplayName
        // Фоновый путь: активатор называет activity по методу задания
        // (например, "MonthlyInvoiceJob.RunAsync"); стабильный ключ источника, когда HTTP-эндпоинт не в области.
        ?? Activity.Current?.OperationName
        ?? "background";

    public AuditTag Tags => AuditTag.None;

    public IAuditScope WithTags(AuditTag tags) => this; // неизменяемое представление
    public IAuditScope WithProperties(string? tenantId = null, string? userId = null, string? userName = null, string? traceId = null,
        string? spanId = null, string? correlationId = null, string? requestId = null, string? source = null, AuditTag? tags = null) => this;

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrEmpty(s) || s == Guid.Empty.ToString() ? null : s;
}
