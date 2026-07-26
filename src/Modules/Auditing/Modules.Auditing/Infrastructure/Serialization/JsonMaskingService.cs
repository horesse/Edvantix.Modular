using EDV.Modules.Auditing.Contracts;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EDV.Modules.Auditing.Infrastructure.Serialization;

/// <summary>
/// Простое маскирование по соглашению об именах полей или атрибутам.
/// </summary>
public sealed class JsonMaskingService : IAuditMaskingService
{
    private static readonly HashSet<string> MaskKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "secret", "token", "otp", "pin",
        "accessToken", "refreshToken", "apiKey", "clientSecret",
        "authCode", "authorization", "bearer", "connectionString"
    };

    private const string MaskValue = "****";

    public MaskingResult ApplyMasking(object payload)
    {
        try
        {
            var json = JsonSerializer.SerializeToNode(payload);
            if (json is null) return new MaskingResult(payload, 0);

            int maskedCount = 0;
            MaskNode(json, ref maskedCount);

            // Ни одно поле не совпало — возвращаем исходную ссылку, чтобы вызывающий код
            // пропустил тег AuditTag.PiiMasked и лишний виток сериализации в sink.
            return maskedCount == 0
                ? new MaskingResult(payload, 0)
                : new MaskingResult(json, maskedCount);
        }
        catch (JsonException)
        {
            return new MaskingResult(payload, 0); // безопасный запасной вариант — payload не является валидным JSON
        }
    }

    private static void MaskNode(JsonNode node, ref int maskedCount)
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj.ToList())
            {
                if (ShouldMask(kvp.Key))
                {
                    obj[kvp.Key] = MaskValue;
                    maskedCount++;
                }
                else if (kvp.Value is not null)
                {
                    MaskNode(kvp.Value, ref maskedCount);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var el in arr)
                if (el is not null) MaskNode(el, ref maskedCount);
        }
    }

    private static bool ShouldMask(string key)
        => MaskKeywords.Any(k => key.Contains(k, StringComparison.OrdinalIgnoreCase));
}
