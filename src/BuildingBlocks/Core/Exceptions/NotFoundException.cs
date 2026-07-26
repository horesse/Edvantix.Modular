using System.Net;

namespace EDV.Framework.Core.Exceptions;

/// <summary>
/// Исключение, представляющее ошибку 404 Not Found (Ресурс не найден).
/// </summary>
public class NotFoundException : CustomException
{
    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="NotFoundException"/> со стандартным сообщением.
    /// </summary>
    public NotFoundException()
        : base("Ресурс не найден.", Array.Empty<string>(), HttpStatusCode.NotFound)
    {
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="NotFoundException"/> с указанным сообщением.
    /// </summary>
    /// <param name="message">Сообщение об ошибке, описывающее, какой ресурс не был найден.</param>
    public NotFoundException(string message)
        : base(message, Array.Empty<string>(), HttpStatusCode.NotFound)
    {
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="NotFoundException"/> 
    /// с сообщением и подробными сведениями об ошибке.
    /// </summary>
    /// <param name="message">Основное сообщение об ошибке.</param>
    /// <param name="errors">Коллекция подробных сообщений об ошибках.</param>
    public NotFoundException(string message, IEnumerable<string> errors)
        : base(message, errors.ToList(), HttpStatusCode.NotFound)
    {
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="NotFoundException"/> 
    /// с сообщением и внутренним исключением.
    /// </summary>
    /// <param name="message">Сообщение об ошибке, описывающее, какой ресурс не был найден.</param>
    /// <param name="innerException">Внутреннее исключение, вызвавшее это исключение.</param>
    public NotFoundException(string message, Exception innerException)
        : base(message, innerException, HttpStatusCode.NotFound)
    {
    }
}