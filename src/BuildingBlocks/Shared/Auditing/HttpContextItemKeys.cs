namespace EDV.Framework.Shared.Auditing;

/// <summary>
/// Общеизвестные ключи <c>HttpContext.Items</c> для сквозных сигналов промежуточного ПО,
/// которые обогащают события активности аудита. Промежуточное ПО-кирпичик записывает эти флаги;
/// аудит HTTP-промежуточное ПО считывает их — это позволяет кирпичикам не зависеть от модуля аудита.
/// </summary>
public static class HttpContextItemKeys
{
    /// <summary>Устанавливается в <c>true</c>, когда запрос был отклонён из-за превышения квоты (HTTP 429).</summary>
    public const string QuotaRejected = "edv.audit.quota-rejected";
}
