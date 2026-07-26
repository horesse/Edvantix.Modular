using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Users.ChangePassword;

public class ChangePasswordCommand : ICommand<string>
{
    /// <summary>Текущий пароль пользователя.</summary>
    public string Password { get; init; } = default!;

    /// <summary>Новый пароль, который пользователь хочет установить.</summary>
    public string NewPassword { get; init; } = default!;

    /// <summary>Подтверждение нового пароля.</summary>
    public string ConfirmNewPassword { get; init; } = default!;
}