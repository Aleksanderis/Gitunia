using System.Text;
using Gitunia.Git.Conflicts.Models;

namespace Gitunia.Git.Conflicts;

/// <summary>
/// Splits the text of a conflicted file into common text and conflict hunks. Understands the three marker styles git
/// writes (<c>merge</c>, <c>diff3</c>, <c>zdiff3</c>) and a marker size other than 7 (<c>conflict-marker-size</c>
/// attribute). A marker only counts inside a hunk or as its opening line, so a line of <c>=======</c> in ordinary text
/// stays text. Unbalanced markers (a second opening line inside a hunk, a missing closing line) are reported as a
/// problem and never guessed at. Pure: no I/O, never throws.
/// </summary>
public static class ConflictParser
{
    /// <summary>Git's default marker size and the shortest run of marker characters accepted.</summary>
    public const int MinimumMarkerSize = 7;

    private const char OursMarker = '<';
    private const char BaseMarker = '|';
    private const char SeparatorMarker = '=';
    private const char TheirsMarker = '>';
    private const char LineFeed = '\n';
    private const char CarriageReturn = '\r';
    private const char Space = ' ';

    private enum Section
    {
        Ours,
        Base,
        Theirs,
    }

    /// <summary>Parses the whole text of a file; joining the result's texts gives the input back.</summary>
    public static ConflictParseResult Parse(string text)
    {
        var parts = new List<ConflictPart>();
        var common = new StringBuilder();
        var position = 0;
        var lineNumber = 0;
        while (position < text.Length)
        {
            var line = NextLine(text, position);
            lineNumber++;
            if (MarkerSize(line, OursMarker) is var size and > 0)
            {
                if (common.Length > 0)
                {
                    parts.Add(ConflictPart.OfCommon(common.ToString()));
                    common.Clear();
                }

                var opening = lineNumber;
                var end = ReadHunk(text, position, size, ref lineNumber, out var hunk, out var problem);
                if (hunk is null)
                {
                    return new ConflictParseResult(null, $"{problem} (the conflict opened on line {opening})");
                }

                parts.Add(ConflictPart.OfHunk(hunk));
                position = end;
                continue;
            }

            common.Append(line);
            position += line.Length;
        }

        if (common.Length > 0)
        {
            parts.Add(ConflictPart.OfCommon(common.ToString()));
        }

        return new ConflictParseResult(parts, null);
    }

    private static int ReadHunk(
        string text,
        int start,
        int size,
        ref int lineNumber,
        out ConflictHunk? hunk,
        out string? problem)
    {
        hunk = null;
        problem = null;
        var opening = NextLine(text, start);
        var oursLabel = LabelOf(opening, size);
        var ours = new StringBuilder();
        var baseText = new StringBuilder();
        var theirs = new StringBuilder();
        var hasBase = false;
        var section = Section.Ours;
        var position = start + opening.Length;
        while (position < text.Length)
        {
            var line = NextLine(text, position);
            lineNumber++;
            position += line.Length;
            if (MarkerSize(line, OursMarker) > 0)
            {
                problem = $"A second conflict opens inside one on line {lineNumber}";
                return position;
            }

            if (section == Section.Ours && MarkerSize(line, BaseMarker) == size)
            {
                section = Section.Base;
                hasBase = true;
            }
            else if (section != Section.Theirs && IsSeparator(line, size))
            {
                section = Section.Theirs;
            }
            else if (section == Section.Theirs && MarkerSize(line, TheirsMarker) == size)
            {
                var raw = text[start..position];
                hunk = new ConflictHunk(
                    ours.ToString(),
                    hasBase ? baseText.ToString() : null,
                    theirs.ToString(),
                    oursLabel,
                    LabelOf(line, size),
                    raw);
                return position;
            }
            else if (MarkerSize(line, TheirsMarker) > 0 && section != Section.Theirs)
            {
                problem = $"The closing marker on line {lineNumber} comes before the separator";
                return position;
            }
            else
            {
                var target = section switch
                {
                    Section.Ours => ours,
                    Section.Base => baseText,
                    _ => theirs,
                };
                target.Append(line);
            }
        }

        problem = "The conflict is never closed";
        return position;
    }

    private static string NextLine(string text, int start)
    {
        var feed = text.IndexOf(LineFeed, start);
        return feed < 0 ? text[start..] : text[start..(feed + 1)];
    }

    // The length of the run of `marker` at the start of the line when what follows makes it a marker line; else 0.
    private static int MarkerSize(string line, char marker)
    {
        var run = 0;
        while (run < line.Length && line[run] == marker)
        {
            run++;
        }

        if (run < MinimumMarkerSize)
        {
            return 0;
        }

        return run == line.Length || line[run] is Space or LineFeed or CarriageReturn ? run : 0;
    }

    // The separator carries no label: the run of equals signs is the whole line.
    private static bool IsSeparator(string line, int size) =>
        MarkerSize(line, SeparatorMarker) == size && line.AsSpan(size).Trim(['\r', '\n']).IsEmpty;

    private static string LabelOf(string markerLine, int size) =>
        markerLine.Length > size ? markerLine[size..].Trim() : string.Empty;
}
