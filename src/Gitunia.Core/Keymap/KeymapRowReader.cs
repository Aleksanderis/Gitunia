using System.Text.Json;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Keymap;

/// <summary>
/// Reads the rows of <c>keymap.default.json</c> with <see cref="Utf8JsonReader"/> (no reflection, Native AOT safe).
/// The file is a JSON array of objects with <c>action</c>, <c>view</c>, <c>modes</c> and <c>keys</c>; other
/// properties (level, label, group, help cluster) are skipped until a consumer reads them. A malformed row throws.
/// </summary>
public static class KeymapRowReader
{
    private const string ActionName = "action";
    private const string ViewName = "view";
    private const string ModesName = "modes";
    private const string KeysName = "keys";

    public static IReadOnlyList<KeymapRow> Read(ReadOnlySpan<byte> utf8)
    {
        var reader = new Utf8JsonReader(
            utf8,
            new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var rows = new List<KeymapRow>();
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("The keymap file must be a JSON array.");
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            rows.Add(ReadRow(ref reader));
        }

        return rows;
    }

    private static KeymapRow ReadRow(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Each keymap row must be an object.");
        }

        string? action = null;
        string? view = null;
        List<KeyMode>? modes = null;
        List<string>? keys = null;

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case ActionName:
                    action = reader.GetString();
                    break;
                case ViewName:
                    view = reader.GetString();
                    break;
                case ModesName:
                    modes = [.. ReadStrings(ref reader).Select(ParseMode)];
                    break;
                case KeysName:
                    keys = ReadStrings(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (action is null || view is null || modes is null || keys is null)
        {
            throw new JsonException("A keymap row needs action, view, modes and keys.");
        }

        return new KeymapRow(new ActionId(action), view, modes, keys);
    }

    private static List<string> ReadStrings(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected a list of strings.");
        }

        var values = new List<string>();
        while (reader.Read() && reader.TokenType == JsonTokenType.String)
        {
            values.Add(reader.GetString()!);
        }

        return values;
    }

    private static KeyMode ParseMode(string text)
    {
        if (!Enum.TryParse<KeyMode>(text, out var mode))
        {
            throw new JsonException($"Unknown mode '{text}'.");
        }

        return mode;
    }
}
