using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Gitunia.Core.Config.Models;

namespace Gitunia.Core.Config;

/// <summary>
/// Sets one key of the config text with a minimal edit: only the value span is replaced or one member is
/// inserted, so comments, order, indentation, line endings and trailing commas stay byte-for-byte. Pure: no file
/// access. A text that does not parse, or a parent that is not an object, is refused and never rewritten.
/// </summary>
public static class ConfigTextEditor
{
    private const string DefaultIndent = "  ";
    private const string Lf = "\n";
    private const string CrLf = "\r\n";

    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The JSON text of a string value (quoted and escaped), for <see cref="Set"/>.</summary>
    /// <param name="value">The string.</param>
    public static string StringValue(string value)
    {
        return $"\"{JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping)}\"";
    }

    /// <summary>Sets the key at <paramref name="path"/> to a JSON value; a missing key is inserted.</summary>
    /// <param name="text">The config text without a byte order mark.</param>
    /// <param name="path">Property names from the root, for example <c>tabs</c>, <c>showBranch</c>.</param>
    /// <param name="jsonValue">One JSON value, for example <c>true</c> or a <see cref="StringValue"/>.</param>
    public static ConfigEditResult Set(string text, IReadOnlyList<string> path, string jsonValue)
    {
        if (path.Count == 0 || !IsSingleValue(jsonValue))
        {
            return Refused(text, "The value is not a single JSON value");
        }

        var result = SetUnchecked(text, path, jsonValue);

        return result.Edited ? Verify(text, result, path, jsonValue) : result;
    }

    private static ConfigEditResult SetUnchecked(string text, IReadOnlyList<string> path, string jsonValue)
    {
        var location = ConfigKeyLocator.Locate(text, path);
        switch (location.Kind)
        {
            case ConfigLocationKind.Found:
                var span = location.Span;
                var replaced = text.Remove(span.Start, span.Length).Insert(span.Start, jsonValue);

                return new ConfigEditResult(true, replaced, string.Empty, location.Shadowed);
            case ConfigLocationKind.Empty:
                return Fill(text, path, jsonValue);
            case ConfigLocationKind.Absent when location.Container is { } container:
                return Insert(text, path, jsonValue, container, location);
            case ConfigLocationKind.Invalid:
                return Refused(text, $"config.json is not valid (line {location.Line}): {location.Message}");
            default:
                return Refused(text, NotObjectMessage(path, location.ResolvedDepth));
        }
    }

    // A last guard: the new text must hold exactly the requested value at the path (never inside a comment).
    private static ConfigEditResult Verify(
        string original,
        ConfigEditResult result,
        IReadOnlyList<string> path,
        string jsonValue)
    {
        var check = ConfigKeyLocator.Locate(result.Text, path);
        var holdsValue = check.Kind == ConfigLocationKind.Found
            && result.Text.AsSpan(check.Span.Start, check.Span.Length).SequenceEqual(jsonValue);

        return holdsValue ? result : Refused(original, "The setting could not be placed safely in config.json");
    }

    private static ConfigEditResult Refused(string text, string message)
    {
        return new ConfigEditResult(false, text, message, false);
    }

    private static string NotObjectMessage(IReadOnlyList<string> path, int depth)
    {
        return depth == 0
            ? "config.json must hold a JSON object at the top level"
            : $"\"{string.Join('.', path.Take(depth))}\" in config.json is not an object";
    }

    private static bool IsSingleValue(string jsonValue)
    {
        try
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonValue));
            if (!reader.Read())
            {
                return false;
            }

            reader.Skip();

            return !reader.Read();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // A blank or comment-only file: keep the comments, add the object after them.
    private static ConfigEditResult Fill(string text, IReadOnlyList<string> path, string jsonValue)
    {
        var lineEnding = LineEnding(text);
        var member = Member(path, 0, jsonValue);
        var objectText = $"{{{lineEnding}{DefaultIndent}{member}{lineEnding}}}{lineEnding}";
        var prefix = text.Length == 0 || text.EndsWith('\n') ? text : $"{text}{lineEnding}";
        var filled = string.IsNullOrWhiteSpace(text) ? objectText : $"{prefix}{objectText}";

        return new ConfigEditResult(true, filled, string.Empty, false);
    }

    private static ConfigEditResult Insert(
        string text,
        IReadOnlyList<string> path,
        string jsonValue,
        TextSpan container,
        ConfigLocation location)
    {
        var member = Member(path, location.ResolvedDepth, jsonValue);
        var lineEnding = LineEnding(text);
        var closeIndex = container.Start + container.Length - 1;
        var scan = ScanMembers(text, container);
        var edited = scan is null
            ? InsertIntoEmpty(text, container, member, lineEnding)
            : InsertAfterLast(text, scan, closeIndex, member, lineEnding);

        return new ConfigEditResult(true, edited, string.Empty, location.Shadowed);
    }

    private static string InsertIntoEmpty(string text, TextSpan container, string member, string lineEnding)
    {
        var lineIndent = LineIndent(text, container.Start);
        var memberIndent = $"{lineIndent}{IndentUnit(text)}";
        var inner = text.Substring(container.Start + 1, container.Length - 2);
        if (string.IsNullOrWhiteSpace(inner))
        {
            var objectText = $"{{{lineEnding}{memberIndent}{member}{lineEnding}{lineIndent}}}";

            return text.Remove(container.Start, container.Length).Insert(container.Start, objectText);
        }

        return text.Insert(container.Start + 1, $"{lineEnding}{memberIndent}{member}");
    }

    private static string InsertAfterLast(
        string text,
        MemberScan scan,
        int closeIndex,
        string member,
        string lineEnding)
    {
        var afterValue = ConfigTrivia.Skip(text, scan.LastValueEnd);
        var hasComma = afterValue < closeIndex && text[afterValue] == ',';
        var gapStart = hasComma ? afterValue + 1 : scan.LastValueEnd;
        var newline = text.LastIndexOf('\n', closeIndex - 1, closeIndex - scan.LastValueEnd);
        var nameLineStart = LineStart(text, scan.LastNameStart);
        var startsLine = scan.LastNameStart == nameLineStart + LineIndent(text, scan.LastNameStart).Length;
        var builder = new StringBuilder(text);
        if (newline >= 0 && startsLine)
        {
            var position = Math.Max(newline > 0 && text[newline - 1] == '\r' ? newline - 1 : newline, gapStart);
            builder.Insert(position, $"{lineEnding}{LineIndent(text, scan.LastNameStart)}{member}");
        }
        else
        {
            builder.Insert(gapStart, hasComma ? $" {member}" : $", {member}");
            return builder.ToString();
        }

        if (!hasComma)
        {
            builder.Insert(scan.LastValueEnd, ',');
        }

        return builder.ToString();
    }

    // The member to add, with the remaining path segments nested as one-line objects.
    private static string Member(IReadOnlyList<string> path, int firstMissing, string jsonValue)
    {
        var value = jsonValue;
        for (var index = path.Count - 1; index > firstMissing; index--)
        {
            value = $"{{ {StringValue(path[index])}: {value} }}";
        }

        return $"{StringValue(path[firstMissing])}: {value}";
    }

    // Where the last member of an object starts and its value ends; null when the object has no members.
    private static MemberScan? ScanMembers(string text, TextSpan container)
    {
        var bytes = Encoding.UTF8.GetBytes(text.Substring(container.Start, container.Length));
        var reader = new Utf8JsonReader(bytes, ReaderOptions);
        reader.Read();
        MemberScan? scan = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var nameStart = (int)reader.TokenStartIndex;
            reader.Read();
            reader.Skip();
            scan = new MemberScan(
                container.Start + Encoding.UTF8.GetCharCount(bytes, 0, nameStart),
                container.Start + Encoding.UTF8.GetCharCount(bytes, 0, (int)reader.BytesConsumed));
        }

        return scan;
    }

    private static string LineEnding(string text)
    {
        return text.Contains(CrLf, StringComparison.Ordinal) ? CrLf : Lf;
    }

    // The whitespace before the first indented content line, so nested inserts follow the file's own style.
    private static string IndentUnit(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimStart(' ', '\t');
            if (trimmed.Length > 0 && trimmed.Length < line.Length && !trimmed.StartsWith('\r'))
            {
                var indent = line[..^trimmed.Length];

                return indent[0] == '\t' ? "\t" : indent;
            }
        }

        return DefaultIndent;
    }

    private static int LineStart(string text, int index)
    {
        return index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
    }

    private static string LineIndent(string text, int index)
    {
        var start = LineStart(text, index);
        var end = start;
        while (end < text.Length && (text[end] == ' ' || text[end] == '\t'))
        {
            end++;
        }

        return text[start..end];
    }

    private sealed record MemberScan(int LastNameStart, int LastValueEnd);
}
