using System.Text.Json;
using System.Text.Json.Serialization;
using Gitunia.Core.Config.Models;

namespace Gitunia.Core.Config;

/// <summary>
/// Reads one <c>keys</c> entry without throwing for a wrongly typed value: a field that is not text, or an entry
/// that is not an object, is recorded in <see cref="ConfigKeyEntry.Problem"/> so only that entry is skipped and
/// reported. Malformed JSON still throws, as it does for the rest of the file. Unknown properties are ignored, names
/// match ignoring case and the last duplicate wins, except that a wrongly typed duplicate still skips the entry.
/// </summary>
public sealed class ConfigKeyEntryConverter : JsonConverter<ConfigKeyEntry>
{
    private const string ModeName = "mode";
    private const string ViewName = "view";
    private const string KeyName = "key";
    private const string ActionName = "action";

    private static readonly string[] FieldNames = [ModeName, ViewName, KeyName, ActionName];

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
        throw new NotSupportedException("The config is only read; ConfigWriter edits the text.");
    }

    private static ConfigKeyEntry Apply(ConfigKeyEntry entry, JsonProperty property)
    {
        var field = property.Value;
        var isText = field.ValueKind is JsonValueKind.String or JsonValueKind.Null;
        var text = field.ValueKind == JsonValueKind.String ? field.GetString() : null;
        var name = property.Name;
        var known = FieldNames.FirstOrDefault(candidate => name.Equals(candidate, StringComparison.OrdinalIgnoreCase));
        if (known is not null && !isText)
        {
            return entry with { Problem = entry.Problem ?? $"'{name}' must be text, not {Describe(field)}" };
        }

        return known switch
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
