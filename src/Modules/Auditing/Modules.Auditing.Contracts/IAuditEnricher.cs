namespace EDV.Modules.Auditing.Contracts;

/// <summary>
/// Хук для дополнения событий перед публикацией (например, добавить арендатора/пользователя/трассировку, нормализовать поля, применить ограничения).
/// </summary>
public interface IAuditEnricher
{
    /// <summary>Изменяет/дополняет экземпляр события перед сериализацией/публикацией.</summary>
    void Enrich(IAuditEvent auditEvent);
}
