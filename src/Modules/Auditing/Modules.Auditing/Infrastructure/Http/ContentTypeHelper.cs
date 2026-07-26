using Microsoft.Net.Http.Headers;

namespace EDV.Modules.Auditing.Infrastructure.Http;

internal static class ContentTypeHelper
{
    public static bool IsJsonLike(string? contentType, ISet<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;

        // Предпочитаем надёжный разбор; при необходимости — упрощённое разбиение.
        if (MediaTypeHeaderValue.TryParse(contentType, out var mt))
            return allowed.Contains(mt.MediaType.Value ?? string.Empty);

        var semi = contentType.IndexOf(';', StringComparison.Ordinal);
        var type = semi >= 0 ? contentType[..semi] : contentType;
        return allowed.Contains(type.Trim());
    }
}