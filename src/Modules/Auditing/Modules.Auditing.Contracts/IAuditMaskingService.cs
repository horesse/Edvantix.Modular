namespace EDV.Modules.Auditing.Contracts;

/// <summary>
/// Результат прохода маскирования по payload. Несёт замаскированный payload
/// и количество полей, которые были скрыты, чтобы вызывающий код мог
/// пометить конверт тегом <see cref="AuditTag.PiiMasked"/> только тогда,
/// когда маскирование действительно применилось (и потребители аудита
/// могут уверенно показывать индикатор "скрыто").
/// </summary>
public readonly record struct MaskingResult(object Payload, int MaskedFieldCount)
{
    public bool Masked => MaskedFieldCount > 0;
}

/// <summary>
/// Маскирует или хеширует чувствительные поля перед сохранением или передачей вовне.
/// </summary>
public interface IAuditMaskingService
{
    /// <summary>
    /// Возвращает замаскированный payload вместе с количеством скрытых полей.
    /// Реализации должны возвращать неизменённую исходную ссылку на payload
    /// (и <c>MaskedFieldCount = 0</c>), если ни одно поле не подошло под
    /// правила маскирования — вызывающий код полагается на счётчик, чтобы
    /// решить, помечать ли конверт тегом <see cref="AuditTag.PiiMasked"/>.
    /// </summary>
    MaskingResult ApplyMasking(object payload);
}
