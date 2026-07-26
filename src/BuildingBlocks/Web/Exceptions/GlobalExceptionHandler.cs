using EDV.Framework.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Diagnostics;

namespace EDV.Framework.Web.Exceptions;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var problemDetails = new ProblemDetails
        {
            Instance = httpContext.Request.Path
        };

        var statusCode = StatusCodes.Status500InternalServerError;

        if (exception is FluentValidation.ValidationException fluentException)
        {
            statusCode = StatusCodes.Status400BadRequest;

            problemDetails.Status = statusCode;
            problemDetails.Title = "Ошибка валидации";
            problemDetails.Detail = "Произошла одна или несколько ошибок валидации.";
            problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";

            var errors = fluentException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            problemDetails.Extensions["errors"] = errors;
        }
        else if (exception is CustomException e)
        {
            statusCode = (int)e.StatusCode;

            problemDetails.Status = statusCode;
            problemDetails.Title = e.GetType().Name;
            problemDetails.Detail = e.Message;

            if (e.ErrorMessages is { Count: > 0 })
            {
                problemDetails.Extensions["errors"] = e.ErrorMessages;
            }
        }
        else if (exception is UnauthorizedAccessException)
        {
            statusCode = StatusCodes.Status401Unauthorized;
            problemDetails.Status = statusCode;
            problemDetails.Title = "Не авторизован";
            problemDetails.Detail = exception.Message;
        }
        else if (exception is KeyNotFoundException)
        {
            statusCode = StatusCodes.Status404NotFound;
            problemDetails.Status = statusCode;
            problemDetails.Title = "Не найдено";
            problemDetails.Detail = exception.Message;
        }
        else if (exception is BadHttpRequestException badRequest)
        {
            // BadHttpRequestException = некорректный запрос (отсутствует обязательный заголовок/параметр, нечитаемое/слишком большое тело).
            // Клиентская ошибка с правильным статусом (обычно 400) — используем его вместо общего 500.
            statusCode = badRequest.StatusCode;
            problemDetails.Status = statusCode;
            problemDetails.Title = "Некорректный запрос";
            problemDetails.Detail = badRequest.Message;
        }
        else
        {
            statusCode = StatusCodes.Status500InternalServerError;
            problemDetails.Status = statusCode;
            problemDetails.Title = "Произошла непредвиденная ошибка";
            problemDetails.Detail = "Произошла непредвиденная ошибка. Пожалуйста, попробуйте позже.";
        }

        httpContext.Response.StatusCode = statusCode;

        // Передаём идентификаторы трассировки и корреляции, чтобы клиенты/поддержка могли сопоставить ошибки с трассировками
        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["correlationId"] = correlationId;

        LogContext.PushProperty("exception_title", problemDetails.Title);
        LogContext.PushProperty("exception_detail", problemDetails.Detail);
        LogContext.PushProperty("exception_statusCode", problemDetails.Status);
        LogContext.PushProperty("exception_stackTrace", exception.StackTrace);

        logger.LogError("Исключение по пути {Path} - {StatusCode} {Title}", httpContext.Request.Path.Value?.Replace(Environment.NewLine, string.Empty), statusCode, problemDetails.Title);

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken).ConfigureAwait(false);
        return true;
    }
}