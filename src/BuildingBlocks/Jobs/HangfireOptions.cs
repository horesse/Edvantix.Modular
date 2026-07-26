using System.ComponentModel.DataAnnotations;

namespace EDV.Framework.Jobs;

public sealed class HangfireOptions
{
    /// <summary>
    /// Имя пользователя, необходимое для доступа к панели управления Hangfire. ОБЯЗАТЕЛЬНО должно быть задано через конфигурацию
    /// в любой среде, кроме разработки — безопасного значения по умолчанию не существует.
    /// </summary>
    [Required]
    [MinLength(3)]
    public string UserName { get; set; } = default!;

    /// <summary>
    /// Пароль, необходимый для доступа к панели управления Hangfire. ОБЯЗАТЕЛЬНО должен быть задан через конфигурацию
    /// (секреты пользователя локально, переменные окружения или Key Vault в продакшене). Короткие или пустые пароли
    /// отклоняются при запуске с помощью <c>ValidateDataAnnotations().ValidateOnStart()</c>.
    /// </summary>
    [Required]
    [MinLength(12)]
    public string Password { get; set; } = default!;

    public string Route { get; set; } = "/jobs";
}