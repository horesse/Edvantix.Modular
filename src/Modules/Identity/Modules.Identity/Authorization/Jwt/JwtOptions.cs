using System.ComponentModel.DataAnnotations;

namespace EDV.Modules.Identity.Authorization.Jwt;

public class JwtOptions : IValidatableObject
{
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 30;
    public int RefreshTokenDays { get; init; } = 7;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(SigningKey))
        {
            yield return new ValidationResult("В JwtOptions не задан ключ (SigningKey)", [nameof(SigningKey)]);
        }

        if (!string.IsNullOrEmpty(SigningKey) && SigningKey.Length < 32)
        {
            yield return new ValidationResult("SigningKey должен содержать не менее 32 символов.", [nameof(SigningKey)]);
        }

        // Отклоняем образец-заполнитель "replace-with-...": если оставить его без изменений,
        // токены могут быть подделаны любым, у кого есть доступ к репозиторию, поэтому отказываемся
        // запускаться, а не выдавать такие токены.
        if (!string.IsNullOrEmpty(SigningKey) &&
            SigningKey.Contains("replace-with", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "SigningKey похож на пример-заполнитель ('replace-with-…'). Установите настоящий секрет через переменную окружения или user-secrets перед запуском хоста.",
                [nameof(SigningKey)]);
        }

        if (string.IsNullOrEmpty(Issuer))
        {
            yield return new ValidationResult("В JwtOptions не задан Issuer", [nameof(Issuer)]);
        }

        if (string.IsNullOrEmpty(Audience))
        {
            yield return new ValidationResult("В JwtOptions не задан Audience", [nameof(Audience)]);
        }
    }
}