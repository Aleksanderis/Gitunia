using System.Text;
using Gitunia.Git.Blame.Models;

namespace Gitunia.Git.Blame;

/// <summary>Turns the bytes of a file into the lines the blame view shows. Pure.</summary>
public static class BlameTextDecoder
{
    /// <summary>Leading bytes searched for a NUL byte to tell binary from text (git looks at 8000).</summary>
    public const int BinaryProbeBytes = 8000;

    private const char LineFeed = '\n';
    private const char CarriageReturn = '\r';
    private const char ByteOrderMark = '﻿';

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    /// Decodes as UTF-8 and, when the bytes are not valid UTF-8, as Latin-1 (every byte maps to a character, so nothing
    /// is lost). Lines end at a line feed; a carriage return before it is dropped. A BOM is dropped. A final line
    /// feed does not start another line, like git counts lines.
    /// </summary>
    /// <param name="bytes">The file.</param>
    public static BlameContent Decode(ReadOnlySpan<byte> bytes)
    {
        var probe = bytes[..Math.Min(bytes.Length, BinaryProbeBytes)];
        if (probe.IndexOf((byte)0) >= 0)
        {
            return new BlameContent(BlameContentKind.Binary, []);
        }

        var text = DecodeText(bytes).TrimStart(ByteOrderMark);
        var lines = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf(LineFeed, start);
            var stop = end < 0 ? text.Length : end;
            var next = end < 0 ? text.Length : end + 1;
            if (stop > start && text[stop - 1] == CarriageReturn)
            {
                stop--;
            }

            lines.Add(text[start..stop]);
            start = next;
        }

        return new BlameContent(BlameContentKind.Text, lines);
    }

    private static string DecodeText(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }
}
