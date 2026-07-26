using EDV.Framework.Persistence;
using EDV.Framework.Shared.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Framework.Tests.Persistence;

public sealed class ConnectionStringValidatorTests
{
    private static ConnectionStringValidator Build(string provider)
    {
        var options = Options.Create(new DatabaseOptions { Provider = provider });
        var logger = Substitute.For<ILogger<ConnectionStringValidator>>();
        return new ConnectionStringValidator(options, logger);
    }

    #region Основной сценарий

    [Fact]
    public void TryValidate_Should_ReturnTrue_When_PostgresConnectionStringValid()
    {
        // Подготовка
        var sut = Build(DbProviders.PostgreSQL);

        // Действие
        var result = sut.TryValidate("Host=localhost;Port=5432;Database=edv;Username=postgres;Password=pwd");

        // Проверка
        result.ShouldBeTrue();
    }

    #endregion

    #region Граничные случаи

    [Fact]
    public void TryValidate_Should_ReturnTrue_When_ProviderUnknown()
    {
        // Подготовка — неизвестный провайдер попадает в ветку по умолчанию без разбора строки.
        var sut = Build("SQLITE");

        // Действие
        var result = sut.TryValidate("any-string");

        // Проверка
        result.ShouldBeTrue();
    }

    [Fact]
    public void TryValidate_Should_ReturnFalse_When_PostgresConnectionStringMalformed()
    {
        // Подготовка
        var sut = Build(DbProviders.PostgreSQL);

        // Действие — неизвестное ключевое слово вызывает ArgumentException в билдере.
        var result = sut.TryValidate("Host=localhost;ThisKeyIsNotValid=oops");

        // Проверка
        result.ShouldBeFalse();
    }

    [Fact]
    public void TryValidate_Should_ReturnTrue_When_EmptyOrWhitespace()
    {
        // Подготовка — билдеры принимают пустую строку/пробелы как валидную (пустую) строку подключения.
        var sut = Build(DbProviders.PostgreSQL);

        // Действие и проверка
        sut.TryValidate(string.Empty).ShouldBeTrue();
        sut.TryValidate("   ").ShouldBeTrue();
    }

    #endregion
}
