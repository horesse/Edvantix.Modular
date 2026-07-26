namespace EDV.Modules.Identity.Data;

public class PasswordPolicyOptions
{
    /// <summary>Количество предыдущих паролей, хранимых в истории (предотвращает повторное использование)</summary>
    public int PasswordHistoryCount { get; set; } = 5;

    /// <summary>Число дней до истечения срока действия пароля, после чего его нужно сменить</summary>
    public int PasswordExpiryDays { get; set; } = 90;

    /// <summary>Число дней до истечения, когда пользователю показывается предупреждение</summary>
    public int PasswordExpiryWarningDays { get; set; } = 14;

    /// <summary>Установите false, чтобы отключить принудительное истечение срока действия пароля</summary>
    public bool EnforcePasswordExpiry { get; set; } = true;
}
