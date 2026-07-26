using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EDV.Modules.Auditing.Infrastructure.Http;

/// <summary>
/// Строит стабильный, удобный для дашборда ключ источника для HTTP-аудита.
///
/// Формат: <c>api.{module}.{routeName}</c> — например, <c>api.identity.RegisterUser</c>.
/// Слаг модуля берётся из сегмента URL после префикса версии
/// (<c>/api/v{n}/{module}/...</c>), а имя маршрута — это явный
/// <c>.WithName(...)</c>, установленный на эндпоинте. Любой из компонентов
/// корректно откатывается при отсутствии — отсутствующее имя маршрута даёт
/// <c>api.{module}</c>; путь вне версионированной формы <c>/api/v{n}/...</c>
/// даёт просто <c>api</c>.
///
/// Стабильные ключи делают фильтры по Source в дашборде аудита пригодными
/// для использования (можно закрепить "покажи мне сбои identity.RegisterUser"
/// без того, чтобы длинное отображаемое имя эндпоинта уплывало при каждом рефакторинге).
/// </summary>
internal static class AuditSourceResolver
{
    public static string Resolve(HttpContext ctx)
    {
        var endpoint = ctx.GetEndpoint();
        var routeName = endpoint?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
        var module = ExtractModuleSlug(ctx.Request.Path.Value);

        return (module, routeName) switch
        {
            (null, null) => "api",
            (null, _) => $"api.{routeName}",
            (_, null) => $"api.{module}",
            _ => $"api.{module}.{routeName}",
        };
    }

    private static string? ExtractModuleSlug(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // Паттерн: /api/v{n}/{module}/...
        // Пропускаем ведущий слэш; нужны минимум три непустых сегмента.
        var segments = path.AsSpan().Trim('/');
        int firstSlash = segments.IndexOf('/');
        if (firstSlash <= 0) return null;
        var seg0 = segments[..firstSlash];

        if (!seg0.Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = segments[(firstSlash + 1)..];
        int secondSlash = rest.IndexOf('/');
        if (secondSlash <= 0) return null;
        var seg1 = rest[..secondSlash];

        if (seg1.IsEmpty || (seg1[0] != 'v' && seg1[0] != 'V'))
        {
            return null;
        }

        var afterVersion = rest[(secondSlash + 1)..];
        int thirdSlash = afterVersion.IndexOf('/');
        var moduleSlug = thirdSlash < 0 ? afterVersion : afterVersion[..thirdSlash];

        return moduleSlug.IsEmpty ? null : moduleSlug.ToString().ToLowerInvariant();
    }
}
