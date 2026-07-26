using EDV.Framework.Storage.Local;

namespace Framework.Tests.Storage;

public sealed class LocalPresignTokenStoreTests
{
    #region Основной сценарий

    [Fact]
    public void IssueThenConsume_Should_ReturnToken_When_NotExpired()
    {
        // Подготовка
        var store = new LocalPresignTokenStore();

        // Действие
        var token = store.Issue("uploads/probe/file.png", "image/png", 2048, TimeSpan.FromMinutes(5));
        var consumed = store.Consume(token);

        // Проверка
        token.ShouldNotBeNullOrWhiteSpace();
        consumed.ShouldNotBeNull();
        consumed!.StorageKey.ShouldBe("uploads/probe/file.png");
        consumed.ContentType.ShouldBe("image/png");
        consumed.MaxBytes.ShouldBe(2048);
    }

    #endregion

    #region Граничные случаи

    [Fact]
    public void Consume_Should_ReturnNull_When_TokenAlreadyConsumed()
    {
        // Подготовка — токены одноразовые.
        var store = new LocalPresignTokenStore();
        var token = store.Issue("k", "text/plain", 1, TimeSpan.FromMinutes(5));

        // Действие
        var first = store.Consume(token);
        var second = store.Consume(token);

        // Проверка
        first.ShouldNotBeNull();
        second.ShouldBeNull();
    }

    [Fact]
    public void Consume_Should_ReturnNull_When_TokenUnknown()
    {
        // Подготовка
        var store = new LocalPresignTokenStore();

        // Действие и проверка
        store.Consume("does-not-exist").ShouldBeNull();
    }

    [Fact]
    public void Consume_Should_ReturnNull_When_TokenExpired()
    {
        // Подготовка — отрицательный ttl делает токен просроченным немедленно.
        var store = new LocalPresignTokenStore();
        var token = store.Issue("k", "text/plain", 1, TimeSpan.FromMinutes(-5));

        // Действие и проверка
        store.Consume(token).ShouldBeNull();
    }

    #endregion
}
