using EDV.Framework.Persistence.Pagination;
using EDV.Framework.Shared.Persistence;

namespace Framework.Tests.Persistence;

public sealed class PaginationExtensionsTests
{
    #region Тестовые дублёры

    private sealed class Item
    {
        public int Value { get; init; }
    }

    private sealed class PagedQuery : IPagedQuery
    {
        public int? PageNumber { get; set; }
        public int? PageSize { get; set; }
        public string? Sort { get; set; }
    }

    private static TestAsyncEnumerable<Item> Source(int count)
        => new(Enumerable.Range(1, count).Select(i => new Item { Value = i }));

    #endregion

    #region Основной сценарий

    [Fact]
    public async Task ToPagedResponseAsync_Should_ReturnRequestedPage_When_ValidParameters()
    {
        // Подготовка
        var query = new PagedQuery { PageNumber = 2, PageSize = 10 };

        // Действие
        var response = await Source(25).ToPagedResponseAsync(query);

        // Проверка
        response.PageNumber.ShouldBe(2);
        response.PageSize.ShouldBe(10);
        response.TotalCount.ShouldBe(25);
        response.TotalPages.ShouldBe(3);
        response.Items.Count.ShouldBe(10);
        response.Items.First().Value.ShouldBe(11);
        response.HasNext.ShouldBeTrue();
        response.HasPrevious.ShouldBeTrue();
    }

    #endregion

    #region Граничные случаи

    [Fact]
    public async Task ToPagedResponseAsync_Should_DefaultPageSizeTo20_When_NotProvided()
    {
        // Подготовка
        var query = new PagedQuery { PageNumber = null, PageSize = null };

        // Действие
        var response = await Source(50).ToPagedResponseAsync(query);

        // Проверка
        response.PageNumber.ShouldBe(1);
        response.PageSize.ShouldBe(20);
        response.Items.Count.ShouldBe(20);
    }

    [Fact]
    public async Task ToPagedResponseAsync_Should_NormalizeNonPositivePage_To1()
    {
        // Подготовка
        var query = new PagedQuery { PageNumber = -5, PageSize = -3 };

        // Действие
        var response = await Source(50).ToPagedResponseAsync(query);

        // Проверка — страница нормализована к 1, размер — к значению по умолчанию 20.
        response.PageNumber.ShouldBe(1);
        response.PageSize.ShouldBe(20);
    }

    [Fact]
    public async Task ToPagedResponseAsync_Should_CapPageSizeAt100_When_Exceeded()
    {
        // Подготовка
        var query = new PagedQuery { PageNumber = 1, PageSize = 500 };

        // Действие
        var response = await Source(120).ToPagedResponseAsync(query);

        // Проверка
        response.PageSize.ShouldBe(100);
        response.Items.Count.ShouldBe(100);
    }

    [Fact]
    public async Task ToPagedResponseAsync_Should_ClampPageToLastPage_When_BeyondRange()
    {
        // Подготовка — 25 элементов, размер 10 => 3 страницы; запрашиваем страницу 99.
        var query = new PagedQuery { PageNumber = 99, PageSize = 10 };

        // Действие
        var response = await Source(25).ToPagedResponseAsync(query);

        // Проверка
        response.PageNumber.ShouldBe(3);
        response.Items.Count.ShouldBe(5);
        response.HasNext.ShouldBeFalse();
    }

    [Fact]
    public async Task ToPagedResponseAsync_Should_ReturnEmpty_When_NoItems()
    {
        // Подготовка
        var query = new PagedQuery { PageNumber = 1, PageSize = 10 };

        // Действие
        var response = await Source(0).ToPagedResponseAsync(query);

        // Проверка
        response.TotalCount.ShouldBe(0);
        response.TotalPages.ShouldBe(0);
        response.Items.ShouldBeEmpty();
        response.HasNext.ShouldBeFalse();
        response.HasPrevious.ShouldBeFalse();
    }

    [Fact]
    public async Task ToPagedResponseAsync_Should_Throw_When_SourceOrPaginationNull()
    {
        // Подготовка
        IQueryable<Item> source = Source(1);

        // Действие и проверка
        await Should.ThrowAsync<ArgumentNullException>(() => source.ToPagedResponseAsync(null!));
        await Should.ThrowAsync<ArgumentNullException>(() =>
            PaginationExtensions.ToPagedResponseAsync<Item>(null!, new PagedQuery()));
    }

    #endregion
}
