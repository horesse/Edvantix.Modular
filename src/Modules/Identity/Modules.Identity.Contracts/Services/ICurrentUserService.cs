using EDV.Framework.Core.Context;

namespace EDV.Modules.Identity.Contracts.Services;

/// <summary>
/// Интерфейс сервиса для управления текущим контекстом пользователя.
/// Объединяет доступ к идентификации пользователя с возможностями инициализации.
/// </summary>
public interface ICurrentUserService : ICurrentUser, ICurrentUserInitializer
{
}