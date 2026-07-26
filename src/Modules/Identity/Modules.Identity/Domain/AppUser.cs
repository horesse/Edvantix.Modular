using EDV.Framework.Core.Domain;
using EDV.Modules.Identity.Domain.Events;
using Microsoft.AspNetCore.Identity;

namespace EDV.Modules.Identity.Domain;

public class AppUser : IdentityUser, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public Uri? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiryTime { get; set; }

    public string? ObjectId { get; set; }

    /// <summary>Время последней смены пароля пользователем</summary>
    public DateTime LastPasswordChangeDate { get; set; } = TimeProvider.System.GetUtcNow().UtcDateTime;

    // Навигационное свойство для истории паролей
    public virtual ICollection<PasswordHistory> PasswordHistories { get; set; } = new List<PasswordHistory>();

    // Реализация IHasDomainEvents
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();
    private void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Фиксирует UserRegisteredEvent. Вызывать после создания пользователя.</summary>
    public void RecordRegistered(string? tenantId = null)
    {
        AddDomainEvent(UserRegisteredEvent.Create(
            userId: Id,
            email: Email ?? string.Empty,
            firstName: FirstName,
            lastName: LastName,
            tenantId: tenantId));
    }

    /// <summary>Фиксирует PasswordChangedEvent. Вызывать после смены пароля.</summary>
    public void RecordPasswordChanged(bool wasReset = false, string? tenantId = null)
    {
        AddDomainEvent(PasswordChangedEvent.Create(
            userId: Id,
            wasReset: wasReset,
            tenantId: tenantId));
    }

    /// <summary>Делает пользователя активным и фиксирует UserActivatedEvent.</summary>
    public void Activate(string? activatedBy = null, string? tenantId = null)
    {
        if (IsActive) return;
        IsActive = true;
        AddDomainEvent(UserActivatedEvent.Create(
            userId: Id,
            activatedBy: activatedBy,
            tenantId: tenantId));
    }

    /// <summary>Делает пользователя неактивным и фиксирует UserDeactivatedEvent.</summary>
    public void Deactivate(string? deactivatedBy = null, string? reason = null, string? tenantId = null)
    {
        if (!IsActive) return;
        IsActive = false;
        AddDomainEvent(UserDeactivatedEvent.Create(
            userId: Id,
            deactivatedBy: deactivatedBy,
            reason: reason,
            tenantId: tenantId));
    }

    /// <summary>Фиксирует UserRoleAssignedEvent. Вызывать после назначения ролей.</summary>
    public void RecordRolesAssigned(IEnumerable<string> assignedRoles, string? tenantId = null)
    {
        var rolesList = assignedRoles.ToList();
        if (rolesList.Count == 0) return;
        AddDomainEvent(UserRoleAssignedEvent.Create(
            userId: Id,
            assignedRoles: rolesList,
            tenantId: tenantId));
    }
}