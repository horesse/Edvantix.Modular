using EDV.Framework.Eventing.Abstractions;

namespace EDV.Framework.Eventing;

/// <summary>
/// No-op реализация <see cref="IEventTenantScope"/>, используемая, если провайдер мультиарендности
/// не подключён. Композиция мультиарендности заменяет её на реализацию на основе Finbuckle.
/// </summary>
public sealed class NullEventTenantScope : IEventTenantScope
{
    private static readonly IDisposable Noop = new NoopScope();

    public IDisposable Begin(string? tenantId) => Noop;

    private sealed class NoopScope : IDisposable
    {
        public void Dispose() { }
    }
}
