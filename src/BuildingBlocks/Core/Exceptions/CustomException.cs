using System.Net;

namespace EDV.Framework.Core.Exceptions;

/// <summary>
/// Исключение, используемое для единообразной обработки ошибок по всему стеку.
/// Включает HTTP-статус коды и необязательные подробные сообщения об ошибках.
/// </summary>
public class CustomException : Exception
{
    /// <summary>
    /// Список сообщений об ошибках (например, ошибки валидации, бизнес-правила).
    /// </summary>
    public IReadOnlyList<string> ErrorMessages { get; }

    /// <summary>
    /// HTTP-статус код, связанный с этим исключением.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="CustomException"/> 
    /// со стандартным сообщением и статусом внутренней ошибки сервера.
    /// </summary>
    public CustomException()
        : this("Произошла ошибка.", Enumerable.Empty<string>())
    {
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="CustomException"/> с указанным сообщением.
    /// </summary>
    /// <param name="message">Сообщение об ошибке.</param>
    public CustomException(string message)
        : this(message, Enumerable.Empty<string>())
    {
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="CustomException"/> 
    /// с указанным сообщением и внутренним исключением.
    /// </summary>
    /// <param name="message">Сообщение об ошибке.</param>
    /// <param name="innerException">Внутреннее исключение, вызвавшее это исключение.</param>
    public CustomException(string message, Exception innerException)
        : this(message, innerException, Enumerable.Empty<string>())
    {
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="CustomException"/> 
    /// с сообщением, списком ошибок и статус кодом.
    /// </summary>
    /// <param name="message">Основное сообщение об ошибке.</param>
    /// <param name="errors">Коллекция подробных сообщений об ошибках.</param>
    /// <param name="statusCode">HTTP-статус код, связанный с этим исключением.</param>
    public CustomException(
        string message,
        IEnumerable<string>? errors,
        HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
        : base(message)
    {
        ErrorMessages = errors?.ToList() ?? new List<string>();
        StatusCode = statusCode;
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="CustomException"/> со всеми параметрами.
    /// </summary>
    /// <param name="message">Основное сообщение об ошибке.</param>
    /// <param name="innerException">Внутреннее исключение, вызвавшее это исключение.</param>
    /// <param name="errors">Коллекция подробных сообщений об ошибках.</param>
    /// <param name="statusCode">HTTP-статус код, связанный с этим исключением.</param>
    public CustomException(
        string message,
        Exception innerException,
        IEnumerable<string>? errors,
        HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
        : base(message, innerException)
    {
        ErrorMessages = errors?.ToList() ?? new List<string>();
        StatusCode = statusCode;
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="CustomException"/> 
    /// с сообщением, внутренним исключением и статус кодом.
    /// </summary>
    /// <param name="message">Основное сообщение об ошибке.</param>
    /// <param name="innerException">Внутреннее исключение, вызвавшее это исключение.</param>
    /// <param name="statusCode">HTTP-статус код, связанный с этим исключением.</param>
    public CustomException(
        string message,
        Exception innerException,
        HttpStatusCode statusCode)
        : this(message, innerException, Enumerable.Empty<string>(), statusCode)
    {
    }
}