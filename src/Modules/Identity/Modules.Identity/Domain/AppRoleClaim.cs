using Microsoft.AspNetCore.Identity;

namespace EDV.Modules.Identity.Domain;

public class AppRoleClaim : IdentityRoleClaim<string>
{
    public string? CreatedBy { get; init; }
    public DateTimeOffset CreatedOn { get; init; }
}