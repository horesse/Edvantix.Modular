namespace EDV.Modules.Auditing.Contracts;

/// <summary>
/// Обогатитель, который может вернуть изменённое событие (например, заполнить недостающие поля, замаскировать payload).
/// </summary>
public interface IAuditMutatingEnricher
{
    AuditEnvelope Enrich(AuditEnvelope envelope);
}
