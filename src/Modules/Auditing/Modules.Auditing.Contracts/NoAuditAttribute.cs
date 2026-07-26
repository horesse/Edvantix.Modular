namespace EDV.Modules.Auditing.Contracts;

/// <summary>
/// Исключает эндпоинт из захвата HTTP-аудита. Применяйте к эндпоинтам, для которых
/// тела запроса/ответа не должны записываться по соображениям комплаенса или
/// приватности (сброс пароля, платёжные формы, регистрация MFA).
///
/// Два режима:
/// <list type="bullet">
///   <item><description><see cref="BodyOnly"/> = false (по умолчанию): полностью
///   пропустить аудит — запись активности не создаётся.</description></item>
///   <item><description><see cref="BodyOnly"/> = true: активность всё же
///   записывается (время, статус, источник, арендатор, пользователь), но
///   превью тела запроса и ответа опускаются.</description></item>
/// </list>
///
/// Применяется через метаданные:
/// <code>
/// endpoints.MapPost("/reset-password", ...)
///     .WithMetadata(new NoAuditAttribute())
///     .RequirePermission(...);
/// </code>
/// или через удобное расширение:
/// <code>endpoints.MapPost(...).NoAudit();</code>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class NoAuditAttribute : Attribute
{
    /// <summary>
    /// Если true, эндпоинт всё равно аудируется, но превью запроса/ответа
    /// опускаются. Если false (по умолчанию), аудит полностью пропускается.
    /// </summary>
    public bool BodyOnly { get; init; }
}
