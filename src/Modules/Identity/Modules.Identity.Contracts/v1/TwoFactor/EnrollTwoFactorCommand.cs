using EDV.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.TwoFactor;

/// <summary>
/// Начинает регистрацию TOTP для текущего пользователя. Генерирует (или заменяет) общий
/// секретный ключ аутентификатора пользователя и возвращает его вместе с URI otpauth://,
/// подходящим для отображения в виде QR-кода. Двухфакторная аутентификация НЕ включается,
/// пока вызывающий не подтвердит код через <see cref="VerifyEnrollTwoFactorCommand"/>.
/// </summary>
public sealed record EnrollTwoFactorCommand : ICommand<TwoFactorEnrollmentResponse>;