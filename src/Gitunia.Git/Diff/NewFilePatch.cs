using System.Text;

namespace Gitunia.Git.Diff;

/// <summary>
/// Writes the patch <c>git diff --no-index -- /dev/null &lt;file&gt;</c> would print for a new file, from the file's bytes. Used for
/// an untracked file whose name is not valid UTF-8 and so cannot be an argument of git. The name is written C-quoted like
/// git does, so the parser reads the bytes back exactly.
/// </summary>
public static class NewFilePatch
{
    private const int BinaryProbeLength = 8000;
    private const byte LineFeed = (byte)'\n';
    private const string NewFileMode = "100644";
    private const string NoNewlineMarker = "\\ No newline at end of file";
    private const int OctalBase = 8;
    private const int OctalDigits = 3;
    private const byte FirstPrintable = 0x20;
    private const byte DeleteCharacter = 0x7F;

    /// <param name="name">The path relative to the working tree, as the bytes on disk.</param>
    /// <param name="content">The bytes of the file.</param>
    public static byte[] Write(ReadOnlySpan<byte> name, ReadOnlySpan<byte> content)
    {
        var quoted = Quote(name, "a/");
        var quotedNew = Quote(name, "b/");
        var output = new List<byte>(content.Length + (name.Length * 4) + 128);
        AppendLine(output, $"diff --git {Encoding.ASCII.GetString(quoted)} {Encoding.ASCII.GetString(quotedNew)}");
        AppendLine(output, $"new file mode {NewFileMode}");
        if (content.IsEmpty)
        {
            return [.. output];
        }

        if (content[..Math.Min(content.Length, BinaryProbeLength)].Contains((byte)0))
        {
            AppendLine(output, $"Binary files /dev/null and {Encoding.ASCII.GetString(quotedNew)} differ");
            return [.. output];
        }

        AppendLine(output, "--- /dev/null");
        AppendLine(output, $"+++ {Encoding.ASCII.GetString(quotedNew)}");
        var lineCount = Count(content, out var endsWithNewline);
        AppendLine(output, lineCount == 1 ? "@@ -0,0 +1 @@" : $"@@ -0,0 +1,{lineCount} @@");
        AppendBody(output, content, endsWithNewline);

        return [.. output];
    }

    private static int Count(ReadOnlySpan<byte> content, out bool endsWithNewline)
    {
        endsWithNewline = content[^1] == LineFeed;
        var count = 0;
        foreach (var value in content)
        {
            if (value == LineFeed)
            {
                count++;
            }
        }

        return endsWithNewline ? count : count + 1;
    }

    private static void AppendBody(List<byte> output, ReadOnlySpan<byte> content, bool endsWithNewline)
    {
        while (!content.IsEmpty)
        {
            var end = content.IndexOf(LineFeed);
            var length = end < 0 ? content.Length : end;
            output.Add((byte)'+');
            output.AddRange(content[..length].ToArray());
            output.Add(LineFeed);
            content = end < 0 ? [] : content[(end + 1)..];
        }

        if (!endsWithNewline)
        {
            AppendLine(output, NoNewlineMarker);
        }
    }

    private static void AppendLine(List<byte> output, string text)
    {
        output.AddRange(Encoding.ASCII.GetBytes(text));
        output.Add(LineFeed);
    }

    /// <summary>The name as git C-quotes it: control bytes, quotes, backslashes and non-ASCII bytes as octal escapes.</summary>
    private static byte[] Quote(ReadOnlySpan<byte> name, string prefix)
    {
        var builder = new StringBuilder("\"").Append(prefix);
        foreach (var value in name)
        {
            if (value < FirstPrintable || value >= DeleteCharacter || value is (byte)'"' or (byte)'\\')
            {
                builder.Append('\\').Append(Convert.ToString(value, OctalBase).PadLeft(OctalDigits, '0'));
            }
            else
            {
                builder.Append((char)value);
            }
        }

        return Encoding.ASCII.GetBytes(builder.Append('"').ToString());
    }
}
