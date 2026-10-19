using System.Text;
using System.Text.Json;
using Gitunia.Core.Config.Models;

namespace Gitunia.Core.Config;

/// <summary>
/// Finds where a value lives in the config text, so a settings screen can replace just that span and keep
/// comments and formatting. A path is property names (matched ignoring case, as the loader does) and array indexes,
/// for example <c>keys</c>, <c>2</c>, <c>action</c>.
/// </summary>
public static class ConfigKeyLocator
{
    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// The span of the value at <paramref name="path"/> (a string value includes its quotes), or null when
    /// absent or the text is invalid.
    /// </summary>
    /// <param name="text">The config text.</param>
    /// <param name="path">Property names and array indexes from the root.</param>
    public static TextSpan? Find(string text, IReadOnlyList<string> path)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        try
        {
            var reader = new Utf8JsonReader(bytes, ReaderOptions);
            if (!reader.Read())
            {
                return null;
            }

            return FindValue(ref reader, path, 0) is { } span ? ToCharacters(text, bytes, span) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The reader is on the first token of a value; returns its byte span when path[depth..] resolves inside it.
    private static TextSpan? FindValue(ref Utf8JsonReader reader, IReadOnlyList<string> path, int depth)
    {
        var start = (int)reader.TokenStartIndex;
        if (depth == path.Count)
        {
            reader.Skip();
            return new TextSpan(start, (int)reader.BytesConsumed - start);
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return FindInObject(ref reader, path, depth);
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            return FindInArray(ref reader, path, depth);
        }

        return null;
    }

    private static TextSpan? FindInObject(ref Utf8JsonReader reader, IReadOnlyList<string> path, int depth)
    {
        TextSpan? found = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString();
            var matches = found is null && string.Equals(name, path[depth], StringComparison.OrdinalIgnoreCase);
            reader.Read();
            if (matches)
            {
                found = FindValue(ref reader, path, depth + 1);
            }
            else
            {
                reader.Skip();
            }
        }

        return found;
    }

    private static TextSpan? FindInArray(ref Utf8JsonReader reader, IReadOnlyList<string> path, int depth)
    {
        TextSpan? found = null;
        var index = 0;
        var wanted = int.TryParse(path[depth], out var parsed) ? parsed : -1;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (index == wanted)
            {
                found = FindValue(ref reader, path, depth + 1);
            }
            else
            {
                reader.Skip();
            }

            index++;
        }

        return found;
    }

    private static TextSpan ToCharacters(string text, byte[] bytes, TextSpan byteSpan)
    {
        var start = Encoding.UTF8.GetCharCount(bytes, 0, byteSpan.Start);
        var length = Encoding.UTF8.GetCharCount(bytes, byteSpan.Start, byteSpan.Length);

        return new TextSpan(Math.Min(start, text.Length), length);
    }
}
