namespace EDV.Modules.Auditing.Persistence;

/// <summary>
/// Транслируемые в LINQ хелперы для запросов к колонке <c>jsonb</c> <c>PayloadJson</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AuditRecord.PayloadJson"/> в модели CLR является <c>string</c>, но отображается на колонку
/// PostgreSQL <c>jsonb</c> (см. <see cref="AuditRecordConfiguration"/>). Прямой вызов
/// <c>EF.Functions.ILike(record.PayloadJson, ...)</c> порождает
/// <c>"PayloadJson" ILIKE @p</c>, что PostgreSQL отклоняет во время выполнения с ошибкой
/// <c>function pg_catalog.like_escape(jsonb, unknown) does not exist</c> — ILIKE принимает
/// только <c>text</c>. Сбой проявляется как HTTP 500, а не как неверный результат.
/// </para>
/// <para>
/// <see cref="AsText"/> отображается на SQL-приведение <c>(jsonb)::text</c> через
/// <c>HasDbFunction(...).HasTranslation(...)</c> в <see cref="AuditDbContext.OnModelCreating"/>,
/// поэтому <c>EF.Functions.ILike(AuditJsonbFunctions.AsText(record.PayloadJson), ...)</c> генерирует
/// валидный, исполняемый SQL: <c>"PayloadJson"::text ILIKE @p</c>.
/// </para>
/// </remarks>
public static class AuditJsonbFunctions
{
    /// <summary>
    /// Приводит колонку payload типа <c>jsonb</c> к <c>text</c>, чтобы её можно было использовать
    /// с текстовыми операторами вроде ILIKE. Действительно только внутри LINQ-запросов EF Core;
    /// выбрасывает исключение при вызове в памяти.
    /// </summary>
    public static string AsText(string payloadJson) =>
        throw new InvalidOperationException(
            $"{nameof(AsText)} — функция только для базы данных и не должна вычисляться на стороне клиента.");
}
