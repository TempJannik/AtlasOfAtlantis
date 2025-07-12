using System.Text.Json;
using System.Text.Json.Serialization;

namespace DOAMapper.Models.Import;

/// <summary>
/// JSON converter that can handle string values that might be provided as numbers or null
/// </summary>
public class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                // Let the JSON deserializer handle Unicode characters naturally
                // If there are encoding issues, they will be handled by the ImportService's encoding detection logic
                return reader.GetString() ?? string.Empty;

            case JsonTokenType.Number:
                // Handle numbers as strings
                if (reader.TryGetInt64(out var longValue))
                {
                    return longValue.ToString();
                }
                if (reader.TryGetDouble(out var doubleValue))
                {
                    return doubleValue.ToString();
                }
                try
                {
                    return reader.GetDecimal().ToString();
                }
                catch
                {
                    return "0";
                }

            case JsonTokenType.True:
                return "true";

            case JsonTokenType.False:
                return "false";

            case JsonTokenType.Null:
                return string.Empty;

            default:
                // For any other type, return empty string
                return string.Empty;
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
