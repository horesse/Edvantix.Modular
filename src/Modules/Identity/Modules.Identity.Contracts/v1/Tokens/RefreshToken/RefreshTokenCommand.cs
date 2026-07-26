using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Tokens.RefreshToken;

// Token — это (возможно, истёкший) access токен, опционально. При наличии обработчик
// проверяет соответствие его субъекта субъекту refresh токена для дополнительной защиты;
// при отсутствии обновление полагается только на валидацию refresh токена.
public record RefreshTokenCommand(string? Token, string RefreshToken)
    : ICommand<RefreshTokenCommandResponse>;