using System.Security.Claims;

namespace EDV.Framework.Shared.Identity.Claims;

public static class ClaimsPrincipalExtensions
{
    // Извлекает claim с email-адресом
    public static string? GetEmail(this ClaimsPrincipal principal) =>
        principal?.FindFirstValue(ClaimTypes.Email);

    // Извлекает claim с идентификатором арендатора
    public static string? GetTenant(this ClaimsPrincipal principal) =>
        principal?.FindFirstValue(CustomClaims.Tenant);

    // Извлекает полное имя пользователя
    public static string? GetFullName(this ClaimsPrincipal principal) =>
        principal?.FindFirstValue(CustomClaims.Fullname);

    // Извлекает имя пользователя
    public static string? GetFirstName(this ClaimsPrincipal principal) =>
        principal?.FindFirstValue(ClaimTypes.Name);

    // Извлекает фамилию пользователя
    public static string? GetSurname(this ClaimsPrincipal principal) =>
        principal?.FindFirstValue(ClaimTypes.Surname);

    // Извлекает номер телефона пользователя
    public static string? GetPhoneNumber(this ClaimsPrincipal principal) =>
        principal?.FindFirstValue(ClaimTypes.MobilePhone);

    // Извлекает идентификатор пользователя
    public static string? GetUserId(this ClaimsPrincipal principal) =>
        principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    // Извлекает URL изображения пользователя в виде Uri
    public static Uri? GetImageUrl(this ClaimsPrincipal principal)
    {
        var imageUrl = principal?.FindFirstValue(CustomClaims.ImageUrl);
        return Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ? uri : null;
    }

    // Извлекает дату истечения срока действия токена пользователя
    public static DateTimeOffset GetExpiration(this ClaimsPrincipal principal)
    {
        var expiration = principal?.FindFirstValue(CustomClaims.Expiration);
        return expiration != null
            ? DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(expiration))
            : throw new InvalidOperationException("Claim с датой истечения не найден.");
    }

    // Вспомогательный метод для извлечения значения claim
    private static string? FindFirstValue(this ClaimsPrincipal principal, string claimType) =>
        principal?.FindFirst(claimType)?.Value;
}