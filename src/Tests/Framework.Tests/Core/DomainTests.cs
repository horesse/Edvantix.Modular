using EDV.Framework.Core.Domain;

namespace Framework.Tests.Core;

public sealed class DomainTests
{
    #region Тестовые дублёры

    private sealed record SampleDomainEvent(Guid EventId, DateTimeOffset OccurredOnUtc, string? CorrelationId = null, string? TenantId = null)
        : DomainEvent(EventId, OccurredOnUtc, CorrelationId, TenantId);

    private sealed class SampleEntity : BaseEntity<Guid>
    {
        public SampleEntity(Guid id) => Id = id;

        public void Raise(IDomainEvent @event) => AddDomainEvent(@event);
    }

    private sealed class SampleAggregate : AggregateRoot<int>
    {
        public SampleAggregate(int id) => Id = id;
    }

    #endregion

    #region BaseEntity

    [Fact]
    public void BaseEntity_Should_ExposeId_When_Constructed()
    {
        // Подготовка
        var id = Guid.NewGuid();

        // Действие
        var entity = new SampleEntity(id);

        // Проверка
        entity.Id.ShouldBe(id);
        entity.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void AddDomainEvent_Should_AppendEvent_When_Raised()
    {
        // Подготовка
        var entity = new SampleEntity(Guid.NewGuid());
        var @event = DomainEvent.Create((id, ts) => new SampleDomainEvent(id, ts));

        // Действие
        entity.Raise(@event);

        // Проверка
        entity.DomainEvents.Count.ShouldBe(1);
        entity.DomainEvents.ShouldContain(@event);
    }

    [Fact]
    public void ClearDomainEvents_Should_EmptyCollection_When_EventsExist()
    {
        // Подготовка
        var entity = new SampleEntity(Guid.NewGuid());
        entity.Raise(DomainEvent.Create((id, ts) => new SampleDomainEvent(id, ts)));
        entity.Raise(DomainEvent.Create((id, ts) => new SampleDomainEvent(id, ts)));

        // Действие
        entity.ClearDomainEvents();

        // Проверка
        entity.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void AggregateRoot_Should_InheritBaseEntityBehavior_When_Constructed()
    {
        // Подготовка и действие
        var aggregate = new SampleAggregate(42);

        // Проверка
        aggregate.Id.ShouldBe(42);
        aggregate.ShouldBeAssignableTo<BaseEntity<int>>();
        aggregate.ShouldBeAssignableTo<IHasDomainEvents>();
    }

    #endregion

    #region DomainEvent

    [Fact]
    public void Create_Should_GenerateIdAndTimestamp_When_FactoryInvoked()
    {
        // Подготовка
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Действие
        var @event = DomainEvent.Create((id, ts) => new SampleDomainEvent(id, ts, "corr", "tenant"));

        // Проверка
        @event.EventId.ShouldNotBe(Guid.Empty);
        @event.OccurredOnUtc.ShouldBeGreaterThan(before);
        @event.CorrelationId.ShouldBe("corr");
        @event.TenantId.ShouldBe("tenant");
    }

    [Fact]
    public void Create_Should_Throw_When_FactoryIsNull()
    {
        // Действие и проверка
        Should.Throw<ArgumentNullException>(() =>
            DomainEvent.Create<SampleDomainEvent>(null!));
    }

    [Fact]
    public void DomainEvent_Should_SupportValueEquality_When_FieldsMatch()
    {
        // Подготовка
        var id = Guid.NewGuid();
        var ts = DateTimeOffset.UtcNow;

        // Действие
        var first = new SampleDomainEvent(id, ts, "c", "t");
        var second = new SampleDomainEvent(id, ts, "c", "t");

        // Проверка
        first.ShouldBe(second);
    }

    #endregion
}
