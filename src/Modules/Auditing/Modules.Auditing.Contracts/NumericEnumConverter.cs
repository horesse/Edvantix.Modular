using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EDV.Modules.Auditing.Contracts;

/// <summary>
/// Заставляет enum сериализоваться как его базовое целое число, даже если
/// зарегистрирован глобальный <see cref="JsonStringEnumConverter"/>. Применяется
/// к <c>[Flags]</c>-перечислениям (<see cref="AuditTag"/>, <see cref="BodyCapture"/>) —
/// битовый набор не является одним именованным значением, а строковая форма конвертера,
/// объединённая через запятую (например, "PiiMasked, Sampled"), ломает потребителей,
/// работающих с битовыми операциями. При чтении принимается целое число или, для
/// защиты, список имён элементов через запятую/пробел.
/// </summary>
public sealed class NumericEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), reader.GetInt64());
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString();
            return string.IsNullOrWhiteSpace(raw)
                ? default
                : Enum.Parse<TEnum>(raw, ignoreCase: true);
        }

        throw new JsonException($"Неожиданный токен {reader.TokenType} при чтении {typeof(TEnum).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
    }
}
