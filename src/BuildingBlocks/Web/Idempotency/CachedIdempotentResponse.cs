using System.ComponentModel;

namespace EDV.Framework.Web.Idempotency;

/// <summary>
/// Кэшированный HTTP-ответ для идемпотентного воспроизведения.
/// </summary>
/// <remarks>
/// Помечен <see cref="ImmutableObjectAttribute"/> + <c>sealed</c>, чтобы HybridCache мог повторно
/// использовать внутрипроцессный экземпляр между запросами без повторной десериализации при каждом попадании в L1.
/// </remarks>
[ImmutableObject(true)]
public sealed record CachedIdempotentResponse
{
    public int StatusCode { get; init; }
    public string? ContentType { get; init; }
    public byte[] Body { get; init; } = [];
}