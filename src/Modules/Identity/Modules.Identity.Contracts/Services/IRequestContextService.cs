using EDV.Framework.Core.Context;

namespace EDV.Modules.Identity.Contracts.Services;

/// <summary>
/// Интерфейс сервиса для доступа к информации контекста HTTP-запроса.
/// Предоставляет метаданные запроса для аудита, логирования и других сквозных задач.
/// </summary>
public interface IRequestContextService : IRequestContext
{
}