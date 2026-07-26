using EDV.Framework.Eventing.Abstractions;
using EDV.Framework.Eventing.Serialization;

namespace Framework.Tests.Eventing;

public sealed class JsonEventSerializerTests
{
    #region Тестовые дублёры

    public sealed record SampleIntegrationEvent : IIntegrationEvent
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
        public string? TenantId { get; init; }
        public string CorrelationId { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string Payload { get; init; } = string.Empty;
    }

    #endregion

    private readonly JsonEventSerializer _sut = new();

    #region Основной сценарий

    [Fact]
    public void SerializeThenDeserialize_Should_RoundTrip_When_TypeResolvable()
    {
        // Подготовка
        var original = new SampleIntegrationEvent
        {
            TenantId = "tenant-1",
            CorrelationId = "corr-1",
            Source = "tests",
            Payload = "hello"
        };
        var typeName = original.GetType().AssemblyQualifiedName!;

        // Действие
        var json = _sut.Serialize(original);
        var roundTripped = _sut.Deserialize(json, typeName);

        // Проверка
        roundTripped.ShouldNotBeNull();
        var typed = roundTripped.ShouldBeOfType<SampleIntegrationEvent>();
        typed.Id.ShouldBe(original.Id);
        typed.TenantId.ShouldBe("tenant-1");
        typed.CorrelationId.ShouldBe("corr-1");
        typed.Payload.ShouldBe("hello");
    }

    [Fact]
    public void Serialize_Should_UseCamelCase_When_Serializing()
    {
        // Подготовка
        var @event = new SampleIntegrationEvent { CorrelationId = "c", Source = "s" };

        // Действие
        var json = _sut.Serialize(@event);

        // Проверка — применена политика именования camelCase (проверка с учётом регистра; PascalCase быть не должно).
        json.ShouldContain("\"correlationId\"");
        json.ShouldContain("\"occurredOnUtc\"");
        json.ShouldNotContain("\"CorrelationId\"", Case.Sensitive);
    }

    #endregion

    #region Граничные случаи

    [Fact]
    public void Deserialize_Should_ReturnNull_When_TypeNameUnresolvable()
    {
        // Подготовка
        var @event = new SampleIntegrationEvent { CorrelationId = "c", Source = "s" };
        var json = _sut.Serialize(@event);

        // Действие
        var result = _sut.Deserialize(json, "Some.Unknown.Type, Nonexistent.Assembly");

        // Проверка
        result.ShouldBeNull();
    }

    [Fact]
    public void Serialize_Should_Throw_When_EventNull()
    {
        Should.Throw<ArgumentNullException>(() => _sut.Serialize(null!));
    }

    [Fact]
    public void Deserialize_Should_Throw_When_PayloadOrTypeNameNull()
    {
        Should.Throw<ArgumentNullException>(() => _sut.Deserialize(null!, "x"));
        Should.Throw<ArgumentNullException>(() => _sut.Deserialize("{}", null!));
    }

    #endregion
}
