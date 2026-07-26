using EDV.Modules.Identity.Contracts.v1.Sessions.RevokeAllSessions;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Sessions.RevokeAllSessions;

public sealed class RevokeAllSessionsCommandValidator : AbstractValidator<RevokeAllSessionsCommand>
{
    public RevokeAllSessionsCommandValidator()
    {
        // ExceptSessionId необязателен - валидация не требуется
        // Этот валидатор существует для единообразия и на случай будущих правил валидации
    }
}