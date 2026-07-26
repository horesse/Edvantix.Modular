namespace EDV.Starter.DbMigrator;

/// <summary>
/// Легковесный парсер командной строки. Избегает использования System.CommandLine для
/// нескольких флагов — оставайтесь честными и минимальными.
///
/// Глаголы:   apply | seed | seed-demo | list-pending  (по умолчанию: apply)
/// Флаги:   --tenant &lt;id&gt;   ограничить одним идентификатором арендатора
///          --catalog-only   пропустить миграции для каждого арендатора
///          --seed           после apply также выполнить SeedAsync для каждого арендатора
///          --help / -h      вывести справочный текст
/// </summary>
internal sealed record MigratorCommand(
    string Command,
    string? Tenant,
    bool CatalogOnly,
    bool SeedAfter,
    bool Help)
{
    private static readonly string[] KnownVerbs = ["apply", "seed", "seed-demo", "list-pending"];

    public static MigratorCommand Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var rawVerb = args.FirstOrDefault(a => !a.StartsWith('-')) ?? "apply";
        // Приводим к известному глаголу через сравнение без учёта регистра (CA1308 запрещает
        // ToLowerInvariant для нормализации, чувствительной к безопасности).
        var verb = KnownVerbs.FirstOrDefault(v => string.Equals(v, rawVerb, StringComparison.OrdinalIgnoreCase))
            ?? rawVerb;

        var tenant = ExtractValue(args, "--tenant");
        var catalogOnly = args.Any(a => string.Equals(a, "--catalog-only", StringComparison.OrdinalIgnoreCase));
        var seedAfter = args.Any(a => string.Equals(a, "--seed", StringComparison.OrdinalIgnoreCase));
        var help = args.Any(a => a is "-h" or "--help");

        return new MigratorCommand(verb, tenant, catalogOnly, seedAfter, help);
    }

    private static string? ExtractValue(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
            // Также принимаем форму --flag=value.
            if (args[i].StartsWith($"{flag}=", StringComparison.OrdinalIgnoreCase))
            {
                return args[i][(flag.Length + 1)..];
            }
        }
        return null;
    }

    public const string HelpText = """
        EDV DbMigrator — применение миграций EF Core по каталогу арендаторов
        и базам данных каждого модуля каждого арендатора.

        Использование:
          dotnet run --project src/Host/EDV.Starter.DbMigrator -- [глагол] [опции]

        Глаголы:
          apply           Применить ожидающие миграции (по умолчанию). Используйте --seed, чтобы также выполнить SeedAsync.
          seed            Выполнить только этап SeedAsync для каждого арендатора.
          seed-demo       Подготовить демо-арендаторов (acme, globex) с пользователями, каталогом,
                          заявками и чатом. Только для разработки — отказывается выполнять, если
                          DOTNET_ENVIRONMENT не равно Development.
          list-pending    Вывести ожидающие миграции без применения.

        Опции:
          --tenant <id>        Ограничиться одним идентификатором арендатора (по умолчанию: все арендаторы).
          --catalog-only       Пропустить проход по арендаторам; мигрируется только каталог арендаторов.
          --seed               После apply также вызвать ITenantService.SeedTenantAsync.
          -h, --help           Вывести этот справочный текст.

        Коды выхода:
          0 — успех
          1 — ошибка (см. залогированное исключение)
        """;
}