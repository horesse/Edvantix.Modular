namespace EDV.Modules.Identity.Domain;

public class PasswordHistory
{
    public int Id { get; init; }
    public string UserId { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }

    // Навигационное свойство (init для материализации EF Core)
    public virtual AppUser? User { get; init; }

    private PasswordHistory() { } // для EF Core

    public static PasswordHistory Create(string userId, string passwordHash)
    {
        return new PasswordHistory
        {
            UserId = userId,
            PasswordHash = passwordHash,
            CreatedAt = TimeProvider.System.GetUtcNow().UtcDateTime
        };
    }
}