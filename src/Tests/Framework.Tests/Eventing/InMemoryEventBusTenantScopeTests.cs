using EDV.Framework.Eventing.Abstractions;
using EDV.Framework.Eventing.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Framework.Tests.Eventing;

/// <summary>
/// Защищает системное исправление ошибки контекста арендатора при фоновой рассылке: шина обязана
/// установить область видимости арендатора по <see cref="IIntegrationEvent.TenantId"/> события
/// ДО того, как разрешит обработчики (которые материализуют DbContext, отфильтрованные по арендатору).
/// Без этого обработчики, вызванные из outbox, получают NRE в своём фильтре запроса по арендатору.
/// </summary>
public sealed class InMemoryEventBusTenantScopeTests
{
    [Fact]
    public async Task PublishAsync_Should_BeginTenantScope_WithEventTenantId_WhileHandlerRuns()
    {
        // Подготовка
        var scope = new RecordingTenantScope();
        var handler = new TenantProbingHandler(scope);

        var services = new ServiceCollection();
        services.AddSingleton<IEventTenantScope>(scope);
        services.AddSingleton<IIntegrationEventHandler<TenantScopedEvent>>(handler);
        using var provider = services.BuildServiceProvider();

        var bus = new InMemoryEventBus(provider, NullLogger<InMemoryEventBus>.Instance, scope);

        // Действие
        await bus.PublishAsync(new TenantScopedEvent("acme"));

        // Проверка — область видимости начата с арендатором события и оставалась активной, пока
        // выполнялся обработчик (т.е. до разрешения зависимостей, восстановлена после).
        scope.BegunWith.ShouldHaveSingleItem().ShouldBe("acme");
        handler.ScopeWasActiveDuringHandle.ShouldBeTrue();
        scope.IsActive.ShouldBeFalse("область видимости должна быть освобождена после завершения рассылки");
    }

    #region Тестовые дублёры

    private sealed record TenantScopedEvent(string? TenantId) : IIntegrationEvent
    {
        public Guid Id { get; } = Guid.CreateVersion7();
        public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
        public string CorrelationId { get; } = Guid.CreateVersion7().ToString();
        public string Source { get; } = "tests";
    }

    private sealed class RecordingTenantScope : IEventTenantScope
    {
        public List<string?> BegunWith { get; } = [];
        public bool IsActive { get; private set; }

        public IDisposable Begin(string? tenantId)
        {
            BegunWith.Add(tenantId);
            IsActive = true;
            return new Handle(this);
        }

        private sealed class Handle(RecordingTenantScope owner) : IDisposable
        {
            public void Dispose() => owner.IsActive = false;
        }
    }

    private sealed class TenantProbingHandler(RecordingTenantScope scope)
        : IIntegrationEventHandler<TenantScopedEvent>
    {
        public bool ScopeWasActiveDuringHandle { get; private set; }

        public Task HandleAsync(TenantScopedEvent @event, CancellationToken ct = default)
        {
            ScopeWasActiveDuringHandle = scope.IsActive;
            return Task.CompletedTask;
        }
    }

    #endregion
}
