using EDV.Framework.Core.Context;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Users.ChangePassword;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Users.ChangePassword;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    private readonly IPasswordHistoryService _passwordHistoryService;
    private readonly ICurrentUser _currentUser;

    public ChangePasswordValidator(
        IPasswordHistoryService passwordHistoryService,
        ICurrentUser currentUser)
    {
        _passwordHistoryService = passwordHistoryService;
        _currentUser = currentUser;

        RuleFor(p => p.Password)
            .NotEmpty()
            .WithMessage("Требуется текущий пароль.");

        RuleFor(p => p.NewPassword)
            .NotEmpty()
            .WithMessage("Требуется новый пароль.")
            .NotEqual(p => p.Password)
            .WithMessage("Новый пароль должен отличаться от текущего.")
            .MustAsync(NotBeInPasswordHistoryAsync)
            .WithMessage("Этот пароль уже использовался недавно. Выберите другой пароль.");

        RuleFor(p => p.ConfirmNewPassword)
            .Equal(p => p.NewPassword)
            .WithMessage("Пароли не совпадают.");
    }

    private async Task<bool> NotBeInPasswordHistoryAsync(string newPassword, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated())
        {
            return true; // Пусть другая валидация обработает неавторизованный доступ
        }

        var userId = _currentUser.GetUserId().ToString();

        // Проверяем, есть ли пароль в истории
        var isInHistory = await _passwordHistoryService.IsPasswordInHistoryAsync(userId, newPassword, cancellationToken);
        return !isInHistory; // Возвращаем true, если пароля НЕТ в истории (валидация проходит)
    }
}