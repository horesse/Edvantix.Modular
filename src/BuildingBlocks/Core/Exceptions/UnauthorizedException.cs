using System.Net;

namespace EDV.Framework.Core.Exceptions;

/// <summary>
/// Исключение, представляющее ошибку 401 Unauthorized (ошибка аутентификации).
/// </summary>
public class UnauthorizedException : CustomException
{
    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="UnauthorizedException"/> со стандартным сообщением.
    /// </summary>
    public UnauthorizedException()
        : base("Ошибка аутентификации.", Array.Empty<string>(), HttpStatusCode.Unauthorized)
    {
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="UnauthorizedException"/> с указанным сообщением.
    /// </summary>
    /// <param name="message">Сообщение об ошибке, описывающее сбой аутентификации.</param>
    public UnauthorizedException(string message)
        : base(message, Array.Empty<string>(), HttpStatusCode.Unauthorized)
    {
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="UnauthorizedException"/> 
    /// с сообщением и подробными сведениями об ошибке.
    /// </summary>
    /// <param name="message">Основное сообщение об ошибке.</param>
    /// <param name="errors">Коллекция подробных сообщений об ошибках.</param>
    public UnauthorizedException(string message, IEnumerable<string> errors)
        : base(message, errors.ToList(), HttpStatusCode.Unauthorized)
    {
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="UnauthorizedException"/> 
    /// с сообщением и внутренним исключением.
    /// </summary>
    /// <param name="message">Сообщение об ошибке, описывающее сбой аутентификации.</param>
    /// <param name="innerException">Внутреннее исключение, вызвавшее это исключение.</param>
    public UnauthorizedException(string message, Exception innerException)
        : base(message, innerException, HttpStatusCode.Unauthorized)
    {
    }
}