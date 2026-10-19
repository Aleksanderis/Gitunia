using System.Text;
using System.Text.Json;
using Gitunia.Core.Config.Models;

namespace Gitunia.Core.Config;

/// <summary>
/// Finds where a value lives in the config text, so the config writer can replace just that span and keep
/// comments and formatting. A path is property names (matched ignoring case, as the loader does) and array indexes,
/// for example <c>keys</c>, <c>2</c>, <c>action</c>. When a member is written more than once the last one wins, as
/// in the loader.
/// </summary>
public static class ConfigKeyLocator
{
    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The span of the value at <paramref name="path"/>, or null unless it was found.</summary>
    /// <param name="text">The config text.</param>
    /// <param name="path">Property names and array indexes from the root.</param>
    public static TextSpan? Find(string text, IReadOnlyList<string> path)
    {
        var location = Locate(text, path);

        return location.Kind == ConfigLocationKind.Found ? location.Span : null;
    }

    /// <summary>Looks the path up and says whether it is found, absent, or why it cannot be.</summary>
    /// <param name="text">The config text.</param>
    /// <param name="path">Property names and array indexes from the root.</param>
    public static ConfigLocation Locate(string text, IReadOnlyList<string> path)
    {
        if (ConfigTrivia.Skip(text, 0) >= text.Length)
        {
            return new ConfigLocation(ConfigLocationKind.Empty);
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        try
        {
            Validate(bytes);
            var reader = new Utf8JsonReader(bytes, ReaderOptions);
            reader.Read();
            var walk = new Walk(path);
            var location = walk.FindValue(ref reader, 0);

            return ToCharacters(text, bytes, location with { Shadowed = walk.Shadowed });
        }
        catch (JsonException exception)
        {
            var line = (int)(exception.LineNumber ?? 0) + 1;

            return new ConfigLocation(ConfigLocationKind.Invalid, Line: line, Message: exception.Message);
        }
    }

    private static void Validate(byte[] bytes)
    {
        var reader = new Utf8JsonReader(bytes, ReaderOptions);
        while (reader.Read())
        {
        }
    }

    private static ConfigLocation ToCharacters(string text, byte[] bytes, ConfigLocation location)
    {
        return location with
        {
            Span = ToCharacters(text, bytes, location.Span),
            Container = location.Container is { } container ? ToCharacters(text, bytes, container) : null,
        };
    }

    private static TextSpan ToCharacters(string text, byte[] bytes, TextSpan byteSpan)
    {
        var start = Encoding.UTF8.GetCharCount(bytes, 0, byteSpan.Start);
        var length = Encoding.UTF8.GetCharCount(bytes, byteSpan.Start, byteSpan.Length);

        return new TextSpan(Math.Min(start, text.Length), length);
    }

    // Walks the document once; spans it produces are in bytes until ToCharacters converts them.
    private sealed class Walk(IReadOnlyList<string> path)
    {
        public bool Shadowed { get; private set; }

        // The reader is on the first token of a value.
        public ConfigLocation FindValue(ref Utf8JsonReader reader, int depth)
        {
            var start = (int)reader.TokenStartIndex;
            if (depth == path.Count)
            {
                reader.Skip();
                var span = new TextSpan(start, (int)reader.BytesConsumed - start);

                return new ConfigLocation(ConfigLocationKind.Found, span);
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                return FindInObject(ref reader, depth);
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                return FindInArray(ref reader, depth);
            }

            return new ConfigLocation(ConfigLocationKind.ParentNotObject, ResolvedDepth: depth);
        }

        private ConfigLocation FindInObject(ref Utf8JsonReader reader, int depth)
        {
            var objectStart = (int)reader.TokenStartIndex;
            ConfigLocation? found = null;
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var matches = string.Equals(reader.GetString(), path[depth], StringComparison.OrdinalIgnoreCase);
                reader.Read();
                if (matches)
                {
                    Shadowed |= found is not null;
                    found = FindValue(ref reader, depth + 1);
                }
                else
                {
                    reader.Skip();
                }
            }

            if (found is not null)
            {
                return found;
            }

            var container = new TextSpan(objectStart, (int)reader.BytesConsumed - objectStart);

            return new ConfigLocation(ConfigLocationKind.Absent, Container: container, ResolvedDepth: depth);
        }

        private ConfigLocation FindInArray(ref Utf8JsonReader reader, int depth)
        {
            ConfigLocation? found = null;
            var index = 0;
            var wanted = int.TryParse(path[depth], out var parsed) ? parsed : -1;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (index == wanted)
                {
                    found = FindValue(ref reader, depth + 1);
                }
                else
                {
                    reader.Skip();
                }

                index++;
            }

            return found ?? new ConfigLocation(ConfigLocationKind.Absent, ResolvedDepth: depth);
        }
    }
}
