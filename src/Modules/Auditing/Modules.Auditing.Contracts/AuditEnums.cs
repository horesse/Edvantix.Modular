using System.Text.Json.Serialization;

namespace EDV.Modules.Auditing.Contracts;

/// <summary>
/// Высокоуровневая классификация событий аудита.
/// </summary>
public enum AuditEventType
{
    None = 0,
    EntityChange = 1,
    Security = 2,
    Activity = 3,
    Exception = 4
}

/// <summary>
/// Шкала серьёзности, согласованная со стандартными уровнями логирования.
/// </summary>
public enum AuditSeverity
{
    None = 0,
    Trace = 1,
    Debug = 2,
    Information = 3,
    Warning = 4,
    Error = 5,
    Critical = 6
}

/// <summary>
/// Действия, связанные с безопасностью, подлежащие отслеживанию (вход, токен, роль и т.д.).
/// </summary>
public enum SecurityAction
{
    None = 0,
    LoginSucceeded = 1,
    LoginFailed = 2,
    TokenIssued = 3,
    TokenRevoked = 4,
    PasswordChanged = 5,
    RoleAssigned = 6,
    RoleRevoked = 7,
    PermissionDenied = 8,
    PolicyFailed = 9,
    ImpersonationStarted = 10,
    ImpersonationEnded = 11
}

/// <summary>
/// Операции с базой данных, которые могут инициировать аудит изменения сущности.
/// </summary>
public enum EntityOperation
{
    None = 0,
    Insert = 1,
    Update = 2,
    Delete = 3,
    SoftDelete = 4,
    Restore = 5
}

/// <summary>
/// Логическая категория событий активности.
/// </summary>
public enum ActivityKind
{
    None = 0,
    Http = 1,
    BackgroundJob = 2,
    Command = 3,
    Query = 4,
    Integration = 5
}

/// <summary>
/// Область или подсистема, в которой возникло исключение.
/// </summary>
public enum ExceptionArea
{
    None = 0,
    Api = 1,
    Worker = 2,
    Ui = 3,
    Infra = 4,
    Unknown = 255
}

/// <summary>
/// Указывает, какие HTTP-тела захватываются в событиях активности.
/// </summary>
[Flags]
[JsonConverter(typeof(NumericEnumConverter<BodyCapture>))]
public enum BodyCapture
{
    None = 0,
    Request = 1,
    Response = 2,
    Both = Request | Response
}

/// <summary>
/// Компактные битовые теги, добавляющие метаданные аудита.
/// </summary>
[Flags]
[JsonConverter(typeof(NumericEnumConverter<AuditTag>))]
public enum AuditTag
{
    None = 0,
    PiiMasked = 1 << 0,
    OutOfQuota = 1 << 1,
    Sampled = 1 << 2,
    RetainedLong = 1 << 3,
    HealthCheck = 1 << 4,
    Authentication = 1 << 5,
    Authorization = 1 << 6
}
