using EDV.Framework.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EDV.Framework.Persistence.Pagination;

/// <summary>
/// Методы расширения для преобразования результатов IQueryable в пагинированные ответы.
/// </summary>
public static class PaginationExtensions
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    /// <summary>
    /// Преобразует IQueryable в пагинированный ответ с указанными параметрами пагинации.
    /// </summary>
    /// <typeparam name="T">Тип элементов в запросе.</typeparam>
    /// <param name="source">Исходный запрос для пагинации.</param>
    /// <param name="pagination">Параметры пагинации, включая номер страницы и размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены для прерывания операции.</param>
    /// <returns>Пагинированный ответ, содержащий запрошенную страницу данных и метаданные пагинации.</returns>
    /// <exception cref="ArgumentNullException">Выбрасывается, когда source или pagination равны null.</exception>
    public static Task<PagedResponse<T>> ToPagedResponseAsync<T>(
        this IQueryable<T> source,
        IPagedQuery pagination,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pagination);

        var pageNumber = pagination.PageNumber is null or <= 0
            ? 1
            : pagination.PageNumber.Value;

        var pageSize = pagination.PageSize is null or <= 0
            ? DefaultPageSize
            : pagination.PageSize.Value;

        if (pageSize > MaxPageSize)
        {
            pageSize = MaxPageSize;
        }

        // Отвязано от спецификаций: предполагается, что source уже содержит необходимую
        // сортировку, применённую через спецификации или явно на месте вызова.
        return ToPagedResponseInternalAsync(source, pageNumber, pageSize, cancellationToken);
    }

    private static async Task<PagedResponse<T>> ToPagedResponseInternalAsync<T>(
        IQueryable<T> source,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
        where T : class
    {
        var totalCount = await source.LongCountAsync(cancellationToken).ConfigureAwait(false);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        if (pageNumber > totalPages && totalPages > 0)
        {
            pageNumber = totalPages;
        }

        var skip = (pageNumber - 1) * pageSize;

        var items = await source
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResponse<T>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }
}