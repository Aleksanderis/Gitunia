using System.Text.Json;
using System.Text.Json.Serialization;
using Gitunia.Core.Config.Models;

namespace Gitunia.Core.Config;

/// <summary>
/// Reads one <c>keys</c> entry without throwing for a wrongly typed value: a field that is not text, or an entry
/// that is not an object, is recorded in <see cref="ConfigKeyEntry.Problem"/> so only that entry is skipped and
/// reported. Malformed JSON still throws, as it does for the rest of the file. Unknown properties are ignored, names
/// match ignoring case and the last duplicate wins, as everywhere in the config.
/// </summary>
public sealed class ConfigKeyEntryConverter : JsonConverter<ConfigKeyEntry>
{
    private const string ModeName = "mode";
    private const string ViewName = "view";
    private const string KeyName = "key";
    private const string ActionName = "action";

    /// <inheritdoc />
    public override ConfigKeyEntry? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var element = JsonElement.ParseValue(ref reader);
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return new ConfigKeyEntry { Problem = $"the entry must be an object, not {Describe(element)}" };
        }

        var entry = new ConfigKeyEntry();
        foreach (var property in element.EnumerateObject())
        {
            entry = Apply(entry, property);
        }

        return entry;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ConfigKeyEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(ModeName, value.Mode);
        writer.WriteString(ViewName, value.View);
        writer.WriteString(KeyName, value.Key);
        writer.WriteString(ActionName, value.Action);
        writer.WriteEndObject();
    }

    private static ConfigKeyEntry Apply(ConfigKeyEntry entry, JsonProperty property)
    {
        var field = property.Value;
        var isText = field.ValueKind is JsonValueKind.String or JsonValueKind.Null;
        var text = field.ValueKind == JsonValueKind.String ? field.GetString() : null;
        var name = property.Name.ToLowerInvariant();
        if (!isText && name is ModeName or ViewName or KeyName or ActionName)
        {
            return entry with { Problem = entry.Problem ?? $"'{property.Name}' must be text, not {Describe(field)}" };
        }

        return name switch
        {
            ModeName => entry with { Mode = text },
            ViewName => entry with { View = text },
            KeyName => entry with { Key = text },
            ActionName => entry with { Action = text },
            _ => entry,
        };
    }

    private static string Describe(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => "an object",
            JsonValueKind.Array => "a list",
            JsonValueKind.Number => "a number",
            JsonValueKind.True or JsonValueKind.False => "a boolean",
            _ => "text",
        };
    }
}
