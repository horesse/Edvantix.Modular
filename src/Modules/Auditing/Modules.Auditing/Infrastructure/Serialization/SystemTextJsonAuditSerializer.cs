using EDV.Modules.Auditing.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EDV.Modules.Auditing.Infrastructure.Serialization;

public sealed class SystemTextJsonAuditSerializer : IAuditSerializer
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false
    };

    public string SerializePayload(object payload) => JsonSerializer.Serialize(payload, Opts);
}