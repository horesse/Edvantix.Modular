using System.Text.Json.Serialization;

namespace EDV.Modules.Identity.Contracts.v1.Impersonation;

public sealed record ImpersonationGrantDto(
    Guid Id,
    string Jti,
    string ActorUserId,
    string? ActorUserName,
    string ActorTenantId,
    string ImpersonatedUserId,
    string? ImpersonatedUserName,
    string ImpersonatedTenantId,
    string Reason,
    DateTime StartedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? EndedAtUtc,
    DateTime? RevokedAtUtc,
    string? RevokedByUserId,
    string? RevokedByUserName,
    string? RevokeReason,
    ImpersonationGrantStatus Status);

// Сериализуется как строка ("Active"/"Ended"/...), а не int, чтобы потребители получали читаемые
// значения, устойчивые к изменению порядка. Аналогично TicketStatus в других местах.
[JsonConverter(typeof(JsonStringEnumConverter<ImpersonationGrantStatus>))]
public enum ImpersonationGrantStatus
{
    /// <summary>Токен действителен и находится в пределах своего времени жизни.</summary>
    Active = 0,
    /// <summary>Оператор нажал Завершить имперсонализацию на панели.</summary>
    Ended = 1,
    /// <summary>Оператор (возможно, не актор) отозвал разрешение.</summary>
    Revoked = 2,
    /// <summary>Токен достиг естественного истечения срока без явного завершения.</summary>
    Expired = 3,
}