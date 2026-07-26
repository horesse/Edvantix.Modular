namespace EDV.Framework.Web.OpenApi;

public sealed class OpenApiOptions
{
    public required string Title { get; init; }
    public string Version { get; init; } = "v1";
    public required string Description { get; init; }

    /// <summary>
    /// Версии API, для которых генерируются документы OpenAPI (например, ["v1", "v2"]).
    /// По умолчанию ["v1"], если не указано.
    /// </summary>
    public string[]? Versions { get; init; }

    public ContactOptions? Contact { get; init; }
    public LicenseOptions? License { get; init; }

    public sealed class ContactOptions
    {
        public string? Name { get; init; }
        public Uri? Url { get; init; }
        public string? Email { get; init; }
    }

    public sealed class LicenseOptions
    {
        public string? Name { get; init; }
        public Uri? Url { get; init; }
    }
}