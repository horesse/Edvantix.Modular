using Microsoft.AspNetCore.Identity;

namespace EDV.Modules.Identity.Domain;

public class AppRole : IdentityRole
{
    public string? Description { get; set; }

    public AppRole(string name, string? description = null)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(name);

        Description = description;
        NormalizedName = name.ToUpperInvariant();
    }
}