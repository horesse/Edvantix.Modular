namespace EDV.Framework.Persistence;

/// <summary>
/// Стабильные имена для именованных фильтров запросов EF Core, регистрируемых
/// фреймворком. Используйте их из мест вызова, где требуется точечно отключить
/// один фильтр (через <c>IgnoreQueryFilters([name])</c>) вместо удаления
/// всех фильтров сущности.
/// </summary>
public static class QueryFilters
{
    /// <summary>
    /// Скрывает строки, где <c>ISoftDeletable.IsDeleted == true</c>. Отключайте
    /// этот фильтр в представлениях корзины и обработчиках восстановления;
    /// ограничения по арендаторам и любые другие фильтры сущности останутся активными.
    /// </summary>
    public const string SoftDelete = nameof(SoftDelete);
}