using EDV.Framework.Core.Domain;

namespace EDV.Modules.Identity.Domain;

/// <summary>
/// Серверная запись одной сессии имперсонации. Создаётся при Start, изменяется
/// при Revoke или естественном End, и ищется по jti при каждом аутентифицированном
/// запросе, несущем claim act_sub. Кэшируемая проверка "отозван или завершён" делает
/// это фактически списком отзыва для токенов имперсонации — что иначе было бы
/// невозможно, поскольку JWT изначально не поддерживают отзыв.
///
/// Реализует <see cref="IGlobalEntity"/>, чтобы отказаться от автоматически
/// применяемого фильтра по арендатору — межарендаторные имперсонации охватывают
/// нескольких арендаторов, поэтому теневая колонка TenantId не отображается чисто.
/// Доступ по арендатору контролируется на уровне запроса через явные фильтры
/// по ActorTenantId / ImpersonatedTenantId.
/// </summary>
public class ImpersonationGrant : IGlobalEntity
{
    public Guid Id { get; private set; }

    /// <summary>JWT id (claim jti) выпущенного токена имперсонации. Уникален.</summary>
    public string Jti { get; private set; } = default!;

    public string ActorUserId { get; private set; } = default!;
    public string? ActorUserName { get; private set; }
    public string ActorTenantId { get; private set; } = default!;

    public string ImpersonatedUserId { get; private set; } = default!;
    public string? ImpersonatedUserName { get; private set; }
    public string ImpersonatedTenantId { get; private set; } = default!;

    public string Reason { get; private set; } = string.Empty;

    public DateTime StartedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Устанавливается, когда оператор нажимает End-impersonation. Токены всё равно истекают естественно, даже если null.</summary>
    public DateTime? EndedAtUtc { get; private set; }

    /// <summary>Устанавливается, когда оператор явно отзывает грант. Отличается от EndedAtUtc.</summary>
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevokedByUserId { get; private set; }
    public string? RevokedByUserName { get; private set; }
    public string? RevokeReason { get; private set; }

    public string? ClientId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    private ImpersonationGrant() { } // для EF Core

    public static ImpersonationGrant Create(
        Guid id,
        string jti,
        string actorUserId,
        string? actorUserName,
        string actorTenantId,
        string impersonatedUserId,
        string? impersonatedUserName,
        string impersonatedTenantId,
        string reason,
        DateTime startedAtUtc,
        DateTime expiresAtUtc,
        string? clientId = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new ImpersonationGrant
        {
            Id = id,
            Jti = jti,
            ActorUserId = actorUserId,
            ActorUserName = actorUserName,
            ActorTenantId = actorTenantId,
            ImpersonatedUserId = impersonatedUserId,
            ImpersonatedUserName = impersonatedUserName,
            ImpersonatedTenantId = impersonatedTenantId,
            Reason = reason,
            StartedAtUtc = startedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            ClientId = clientId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
        };
    }

    /// <summary>Помечает как естественно завершённый (оператор нажал End-impersonation). No-op, если уже терминален.</summary>
    public void MarkEnded(DateTime endedAtUtc)
    {
        if (IsTerminal) return;
        EndedAtUtc = endedAtUtc;
    }

    /// <summary>Отзывает этот грант. No-op, если уже терминален.</summary>
    public void Revoke(DateTime revokedAtUtc, string revokedByUserId, string? revokedByUserName, string? reason)
    {
        if (IsTerminal) return;
        RevokedAtUtc = revokedAtUtc;
        RevokedByUserId = revokedByUserId;
        RevokedByUserName = revokedByUserName;
        RevokeReason = reason;
    }

    public bool IsTerminal => EndedAtUtc.HasValue || RevokedAtUtc.HasValue;
    public bool IsRevoked => RevokedAtUtc.HasValue;
}
