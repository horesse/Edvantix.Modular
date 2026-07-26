using EDV.Framework.Eventing.Abstractions;
using EDV.Framework.Shared.Multitenancy;
using Finbuckle.MultiTenant.Abstractions;

namespace EDV.Modules.Multitenancy.Services;

/// <summary>
/// Реализация <see cref="IEventTenantScope"/> на основе Finbuckle. Устанавливает окружающий контекст тенанта
/// (AsyncLocal в Finbuckle) на время диспетчеризации интеграционного события, чтобы DbContext-ы,
/// разрешаемые впоследствии в обработчиках, получали настоящий <c>TenantInfo</c>, а не null,
/// который иначе нёс бы фоновый scope.
///
/// Повторяет уже используемый паттерн "создать scope, затем задать тенанта" из
/// <c>WebhookDispatchJob</c> / <c>SqlAuditSink</c>, обобщённый для конвейера событий.
/// Устанавливается только идентификатор тенанта (этого достаточно для построчного фильтра
/// по тенанту в модели с общей базой данных); строки подключения для конкретных тенантов здесь не разрешаются.
/// </summary>
public sealed class FinbuckleEventTenantScope : IEventTenantScope
{
    private readonly IMultiTenantContextAccessor<AppTenantInfo> _accessor;
    private readonly IMultiTenantContextSetter _setter;

    public FinbuckleEventTenantScope(
        IMultiTenantContextAccessor<AppTenantInfo> accessor,
        IMultiTenantContextSetter setter)
    {
        _accessor = accessor;
        _setter = setter;
    }

    public IDisposable Begin(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            // Глобальные события: не трогаем окружающий контекст.
            return NoopScope.Instance;
        }

        var previous = _accessor.MultiTenantContext;
        _setter.MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(new AppTenantInfo(tenantId, tenantId));

        return new RestoreScope(_setter, previous);
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly IMultiTenantContextSetter _setter;
        private readonly IMultiTenantContext<AppTenantInfo> _previous;

        public RestoreScope(IMultiTenantContextSetter setter, IMultiTenantContext<AppTenantInfo> previous)
        {
            _setter = setter;
            _previous = previous;
        }

        public void Dispose() => _setter.MultiTenantContext = _previous;
    }

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();
        public void Dispose() { }
    }
}
