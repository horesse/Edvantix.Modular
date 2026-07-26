namespace EDV.Modules.Identity.Domain;

public class UserGroup
{
    public string UserId { get; private set; } = default!;
    public Guid GroupId { get; private set; }
    public DateTime AddedAt { get; private set; }
    public string? AddedBy { get; private set; }

    // Навигационные свойства (init для материализации EF Core)
    public virtual AppUser? User { get; init; }
    public virtual Group? Group { get; init; }

    private UserGroup() { } // для EF Core

    public static UserGroup Create(string userId, Guid groupId, string? addedBy = null)
    {
        return new UserGroup
        {
            UserId = userId,
            GroupId = groupId,
            AddedAt = TimeProvider.System.GetUtcNow().UtcDateTime,
            AddedBy = addedBy
        };
    }
}