namespace EDV.Framework.Shared.Multitenancy;

/// <summary>
/// Кратковременный внутрипроцессный буфер, который передаёт начальный пароль администратора
/// из обработки <c>CreateTenantCommand</c> через фоновый конвейер подготовки,
/// пока <c>IdentityDbInitializer.SeedAdminUserAsync</c> не потребит его.
///
/// Пароль никогда не сохраняется в <c>AppTenantInfo</c> — эта запись доступна
/// для чтения в любом месте процесса приложения, а пароль администратора арендатора
/// не должен находиться нигде, кроме как в хешированном виде внутри <c>AspNetUsers</c>.
///
/// Находится в <c>Shared</c>, чтобы модуль Identity (потребитель) и
/// модуль Multitenancy (производитель + реализатор) могли зависеть от абстракции,
/// не имея прямой ссылки друг на друга во время выполнения.
/// </summary>
public interface ITenantInitialPasswordBuffer
{
    /// <summary>Помещает пароль в буфер для идентификатора арендатора. Перезаписывает предыдущее значение.</summary>
    void Store(string tenantId, string password);

    /// <summary>Атомарно считывает и удаляет буферизированный пароль для идентификатора арендатора.</summary>
    string? TryConsume(string tenantId);
}