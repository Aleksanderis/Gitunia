using System.Buffers;
using System.Text;

namespace Gitunia.Git.Execution;

/// <summary>
/// Lossless text form of git output that may hold file names that are not UTF-8 (valid on Linux). Valid UTF-8 decodes
/// as usual; every invalid byte <c>b</c> becomes the lone low surrogate <c>U+DC00 + b</c> (the "surrogateescape" scheme),
/// which valid UTF-8 never produces, so <see cref="Encode"/> restores the original bytes exactly. Such strings must not
/// be passed as process arguments (the runtime would replace the surrogates); hand paths to git on stdin with
/// <see cref="EncodePathspecFile"/> and <see cref="GitOptions.PathspecFromStandardInput"/> instead.
/// </summary>
public static class GitPathText
{
    private const int EscapeBase = 0xDC00;

    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        var builder = new StringBuilder(bytes.Length);
        while (bytes.Length > 0)
        {
            var status = Rune.DecodeFromUtf8(bytes, out var rune, out var consumed);
            if (status == OperationStatus.Done)
            {
                builder.Append(rune.ToString());
            }
            else
            {
                consumed = Math.Max(consumed, 1);
                for (var index = 0; index < consumed; index++)
                {
                    builder.Append((char)(EscapeBase + bytes[index]));
                }
            }

            bytes = bytes[consumed..];
        }

        return builder.ToString();
    }

    public static byte[] Encode(string text)
    {
        var bytes = new List<byte>(text.Length);
        Span<byte> buffer = stackalloc byte[4];
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (char.IsLowSurrogate(character))
            {
                // A low surrogate after a high one was consumed with it below, so this one is an escaped byte.
                bytes.Add((byte)(character - EscapeBase));
            }
            else if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                var written = new Rune(character, text[index + 1]).EncodeToUtf8(buffer);
                bytes.AddRange(buffer[..written].ToArray());
                index++;
            }
            else if (char.IsHighSurrogate(character))
            {
                throw new ArgumentException("Text holds a lone high surrogate, so it is not a path produced by Decode.", nameof(text));
            }
            else
            {
                var written = new Rune(character).EncodeToUtf8(buffer);
                bytes.AddRange(buffer[..written].ToArray());
            }
        }

        return [.. bytes];
    }

    /// <summary>True if <paramref name="text"/> holds escaped (non-UTF-8) bytes, so it cannot be a process argument.</summary>
    public static bool HasEscapedBytes(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                index++;
            }
            else if (char.IsSurrogate(text[index]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Stdin content for <c>--pathspec-from-file=-  --pathspec-file-nul</c>: every path followed by NUL, bytes exact.</summary>
    public static byte[] EncodePathspecFile(IEnumerable<string> paths)
    {
        var bytes = new List<byte>();
        foreach (var path in paths)
        {
            bytes.AddRange(Encode(path));
            bytes.Add(0);
        }

        return [.. bytes];
    }
}
