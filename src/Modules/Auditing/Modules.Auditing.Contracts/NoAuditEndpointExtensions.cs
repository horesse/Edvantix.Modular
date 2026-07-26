using Microsoft.AspNetCore.Builder;

namespace EDV.Modules.Auditing.Contracts;

/// <summary>
/// Удобные билдеры для пометки эндпоинтов атрибутом <see cref="NoAuditAttribute"/>.
/// </summary>
public static class NoAuditEndpointExtensions
{
    /// <summary>
    /// Полностью подавляет HTTP-аудит для этого эндпоинта. Используйте для маршрутов,
    /// где сам факт вызова является чувствительной информацией.
    /// </summary>
    public static TBuilder NoAudit<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new NoAuditAttribute { BodyOnly = false });
    }

    /// <summary>
    /// Записывает активность, но опускает превью тела запроса/ответа. Используйте,
    /// когда нужна видимость времени/статуса, но тела содержат PII.
    /// </summary>
    public static TBuilder NoAuditBody<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new NoAuditAttribute { BodyOnly = true });
    }
}
