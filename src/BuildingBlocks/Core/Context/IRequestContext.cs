namespace EDV.Framework.Core.Context;

/// <summary>
/// Предоставляет доступ к информации контекста HTTP-запроса без прямой зависимости от ASP.NET Core.
/// Используйте этот интерфейс в обработчиках, которым необходимы метаданные запроса для аудита, логирования и т. д.
/// </summary>
public interface IRequestContext
{
    /// <summary>
    /// Возвращает удалённый IP-адрес клиента, выполняющего запрос.
    /// </summary>
    string? IpAddress { get; }

    /// <summary>
    /// Возвращает заголовок User-Agent из запроса.
    /// </summary>
    string? UserAgent { get; }

    /// <summary>
    /// Возвращает идентификатор клиента из заголовка X-Client-Id или значение по умолчанию.
    /// </summary>
    string ClientId { get; }

    /// <summary>
    /// Возвращает URL источника (схема + хост + базовый путь) текущего запроса.
    /// </summary>
    string? Origin { get; }
}