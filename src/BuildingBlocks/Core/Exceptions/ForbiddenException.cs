using System.Net;

namespace EDV.Framework.Core.Exceptions;

/// <summary>
/// Исключение, представляющее ошибку 403 Forbidden (Доступ запрещён).
/// </summary>
public class ForbiddenException : CustomException
{
    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="ForbiddenException"/> со стандартным сообщением.
    /// </summary>
    public ForbiddenException()
        : base("Несанкционированный доступ.", Array.Empty<string>(), HttpStatusCode.Forbidden)
    {
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="ForbiddenException"/> с указанным сообщением.
    /// </summary>
    /// <param name="message">Сообщение об ошибке, описывающее запрещённое действие.</param>
    public ForbiddenException(string message)
        : base(message, Array.Empty<string>(), HttpStatusCode.Forbidden)
    {
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="ForbiddenException"/> 
    /// с сообщением и подробными сведениями об ошибке.
    /// </summary>
    /// <param name="message">Основное сообщение об ошибке.</param>
    /// <param name="errors">Коллекция подробных сообщений об ошибках.</param>
    public ForbiddenException(string message, IEnumerable<string> errors)
        : base(message, errors.ToList(), HttpStatusCode.Forbidden)
    {
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="ForbiddenException"/> 
    /// с сообщением и внутренним исключением.
    /// </summary>
    /// <param name="message">Сообщение об ошибке, описывающее запрещённое действие.</param>
    /// <param name="innerException">Внутреннее исключение, вызвавшее это исключение.</param>
    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException, HttpStatusCode.Forbidden)
    {
    }
}