namespace EDV.Modules.Identity.Contracts.DTOs;

/// <summary>
/// Возвращается из конечной точки регистрации. <see cref="SharedKey"/> — это секрет TOTP в кодировке base32
/// (отображается для пользователей, которые не могут отсканировать QR-код). <see cref="AuthenticatorUri"/> —
/// стандартный URI otpauth://, подходящий для отображения в виде QR-кода.
/// </summary>
public sealed record TwoFactorEnrollmentResponse(string SharedKey, string AuthenticatorUri);