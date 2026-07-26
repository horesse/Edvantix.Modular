using EDV.Framework.Core.Exceptions;
using System.Net;

namespace Framework.Tests.Core;

public sealed class ExceptionsTests
{
    #region CustomException

    [Fact]
    public void Ctor_Should_DefaultToInternalServerError_When_ParameterlessUsed()
    {
        // Подготовка и действие
        var exception = new CustomException();

        // Проверка
        exception.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        exception.Message.ShouldBe("Произошла ошибка.");
        exception.ErrorMessages.ShouldBeEmpty();
    }

    [Fact]
    public void Ctor_Should_SetMessage_When_MessageOnlyProvided()
    {
        // Подготовка и действие
        var exception = new CustomException("boom");

        // Проверка
        exception.Message.ShouldBe("boom");
        exception.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        exception.ErrorMessages.ShouldBeEmpty();
    }

    [Fact]
    public void Ctor_Should_SetErrorsAndStatusCode_When_FullArgsProvided()
    {
        // Подготовка
        var errors = new[] { "first", "second" };

        // Действие
        var exception = new CustomException("bad request", errors, HttpStatusCode.BadRequest);

        // Проверка
        exception.Message.ShouldBe("bad request");
        exception.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        exception.ErrorMessages.Count.ShouldBe(2);
        exception.ErrorMessages.ShouldContain("first");
        exception.ErrorMessages.ShouldContain("second");
    }

    [Fact]
    public void Ctor_Should_DefaultToEmptyErrors_When_NullErrorsProvided()
    {
        // Подготовка и действие
        var exception = new CustomException("msg", errors: null, HttpStatusCode.Conflict);

        // Проверка
        exception.ErrorMessages.ShouldNotBeNull();
        exception.ErrorMessages.ShouldBeEmpty();
        exception.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public void Ctor_Should_PreserveInnerException_When_InnerProvided()
    {
        // Подготовка
        var inner = new InvalidOperationException("inner");

        // Действие
        var exception = new CustomException("outer", inner, HttpStatusCode.ServiceUnavailable);

        // Проверка
        exception.InnerException.ShouldBeSameAs(inner);
        exception.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        exception.ErrorMessages.ShouldBeEmpty();
    }

    #endregion

    #region NotFoundException

    [Fact]
    public void NotFoundException_Should_Map404_When_Constructed()
    {
        // Подготовка и действие
        var defaultException = new NotFoundException();
        var messageException = new NotFoundException("missing user");
        var errorsException = new NotFoundException("missing", new[] { "id=1" });

        // Проверка
        defaultException.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        defaultException.Message.ShouldBe("Ресурс не найден.");
        messageException.Message.ShouldBe("missing user");
        messageException.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        errorsException.ErrorMessages.ShouldContain("id=1");
    }

    [Fact]
    public void NotFoundException_Should_BeCustomException_When_TypeChecked()
    {
        // Подготовка и действие
        var exception = new NotFoundException();

        // Проверка
        exception.ShouldBeAssignableTo<CustomException>();
    }

    #endregion

    #region ForbiddenException

    [Fact]
    public void ForbiddenException_Should_Map403_When_Constructed()
    {
        // Подготовка и действие
        var defaultException = new ForbiddenException();
        var messageException = new ForbiddenException("no access");

        // Проверка
        defaultException.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        defaultException.Message.ShouldBe("Несанкционированный доступ.");
        messageException.Message.ShouldBe("no access");
        messageException.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region UnauthorizedException

    [Fact]
    public void UnauthorizedException_Should_Map401_When_Constructed()
    {
        // Подготовка и действие
        var defaultException = new UnauthorizedException();
        var errorsException = new UnauthorizedException("login failed", new[] { "expired token" });

        // Проверка
        defaultException.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        defaultException.Message.ShouldBe("Ошибка аутентификации.");
        errorsException.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        errorsException.ErrorMessages.ShouldContain("expired token");
    }

    #endregion
}
