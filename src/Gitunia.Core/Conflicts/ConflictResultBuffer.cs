using Gitunia.Core.Editing;
using Gitunia.Git.Conflicts.Models;

namespace Gitunia.Core.Conflicts;

/// <summary>
/// The result pane's text: the whole file in a <see cref="MessageEditor"/> (the commit box's text editor) plus where
/// each conflict sits in it. A conflict starts with its markers (unresolved); a pick swaps the region's lines for the
/// chosen ones, and typing anywhere in the editor is followed by <see cref="Reconcile"/>, which finds the lines that
/// changed and moves the regions after them (a region the change touches becomes <see cref="ConflictPick.Edited"/>).
/// Mutable, owned by one <see cref="ConflictEditorState"/> chain; undo works on <see cref="Capture"/> snapshots.
/// </summary>
public sealed class ConflictResultBuffer
{
    private const string OpeningMarker = "<<<<<<<";
    private const string ClosingMarker = ">>>>>>>";
    private const string SeparatorMarker = "=======";

    private IReadOnlyList<ConflictRegion> _regions;
    private string[] _shadow;

    private ConflictResultBuffer(MessageEditor editor, IReadOnlyList<ConflictRegion> regions)
    {
        Editor = editor;
        _regions = regions;
        _shadow = [.. editor.Lines];
    }

    /// <summary>The text editor holding the result.</summary>
    public MessageEditor Editor { get; }

    /// <summary>Where each conflict sits, in file order.</summary>
    public IReadOnlyList<ConflictRegion> Regions => _regions;

    /// <summary>The buffer for a freshly read file: every conflict unresolved, markers in place.</summary>
    public static ConflictResultBuffer Create(IReadOnlyList<ConflictPart> parts)
    {
        var lines = new List<string>();
        var regions = new List<ConflictRegion>();
        foreach (var part in parts)
        {
            if (part.Hunk is { } hunk)
            {
                var raw = ConflictAlignment.Lines(hunk.Raw);
                regions.Add(new ConflictRegion(lines.Count, raw.Count, ConflictPick.None));
                lines.AddRange(raw);
            }
            else
            {
                lines.AddRange(ConflictAlignment.Lines(part.Common ?? string.Empty));
            }
        }

        var editor = new MessageEditor { UsesTabCharacters = true };
        editor.LoadLines(lines);

        return new ConflictResultBuffer(editor, regions);
    }

    /// <summary>The lines a region holds now.</summary>
    public IReadOnlyList<string> LinesOf(int hunk)
    {
        var region = _regions[hunk];

        return [.. Enumerable.Range(region.Start, region.Length).Select(line => Editor.Lines[line])];
    }

    /// <summary>
    /// True while the conflict is not settled: never picked, or typed on and still holding a conflict marker line.
    /// </summary>
    public bool IsUnresolved(int hunk)
    {
        var region = _regions[hunk];
        if (region.Pick == ConflictPick.None)
        {
            return true;
        }

        if (region.Pick != ConflictPick.Edited)
        {
            return false;
        }

        for (var line = region.Start; line < region.End; line++)
        {
            if (IsMarker(Editor.Lines[line]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The conflict whose region holds a result line, -1 for none (an empty region holds no line).</summary>
    public int RegionAt(int line)
    {
        for (var hunk = 0; hunk < _regions.Count; hunk++)
        {
            if (line >= _regions[hunk].Start && line < _regions[hunk].End)
            {
                return hunk;
            }
        }

        return -1;
    }

    /// <summary>Gives a region new lines and a pick; the regions after it move with the text.</summary>
    public void ReplaceRegion(int hunk, IReadOnlyList<string> lines, ConflictPick pick)
    {
        var region = _regions[hunk];
        Editor.ReplaceLines(region.Start, region.Length, lines);
        var delta = lines.Count - region.Length;
        var regions = new List<ConflictRegion>(_regions);
        regions[hunk] = new ConflictRegion(region.Start, lines.Count, pick);
        for (var later = hunk + 1; later < regions.Count; later++)
        {
            regions[later] = regions[later] with { Start = regions[later].Start + delta };
        }

        _regions = regions;
        _shadow = [.. Editor.Lines];
    }

    /// <summary>
    /// Call after the editor changed its text (a key, a Normal mode change): compares the lines with those before,
    /// moves the regions after the changed lines and marks a region the change touched as edited. Insertions just
    /// before or just after a region stay outside it.
    /// </summary>
    public void Reconcile()
    {
        var lines = Editor.Lines;
        var oldCount = _shadow.Length;
        var newCount = lines.Count;
        var prefix = 0;
        var shortest = Math.Min(oldCount, newCount);
        while (prefix < shortest && string.Equals(_shadow[prefix], lines[prefix], StringComparison.Ordinal))
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < shortest - prefix
            && string.Equals(_shadow[oldCount - 1 - suffix], lines[newCount - 1 - suffix], StringComparison.Ordinal))
        {
            suffix++;
        }

        var oldEnd = oldCount - suffix;
        var delta = newCount - oldCount;
        if (delta != 0 || oldEnd > prefix || newCount - suffix > prefix)
        {
            _regions = [.. _regions.Select(region => Moved(region, prefix, oldEnd, delta))];
        }

        _shadow = [.. lines];
    }

    /// <summary>The state to restore on undo or redo.</summary>
    public ConflictBufferSnapshot Capture() =>
        new([.. Editor.Lines], _regions, Editor.CursorRow, Editor.CursorColumn);

    /// <summary>Puts the text, regions and cursor of a snapshot back.</summary>
    public void Restore(ConflictBufferSnapshot snapshot)
    {
        Editor.LoadLines(snapshot.Lines);
        Editor.SetCursor(snapshot.Row, snapshot.Column);
        _regions = snapshot.Regions;
        _shadow = [.. Editor.Lines];
    }

    /// <summary>True when the lines and regions equal those of a snapshot.</summary>
    public bool Matches(ConflictBufferSnapshot snapshot) =>
        Editor.Lines.Count == snapshot.Lines.Length
        && Editor.Lines.SequenceEqual(snapshot.Lines, StringComparer.Ordinal)
        && _regions.SequenceEqual(snapshot.Regions);

    /// <summary>
    /// The whole result as a file: lines joined with the line ending, a final one when the file had it.
    /// </summary>
    /// <param name="lineEnding">The line ending to write.</param>
    /// <param name="hasFinalNewline">Whether the file ended with a line break.</param>
    public string ToText(string lineEnding, bool hasFinalNewline)
    {
        var lines = Editor.Lines;
        if (lines.Count == 1 && lines[0].Length == 0)
        {
            return string.Empty;
        }

        var text = string.Join(lineEnding, lines);

        return hasFinalNewline ? $"{text}{lineEnding}" : text;
    }

    private static ConflictRegion Moved(ConflictRegion region, int prefix, int oldEnd, int delta)
    {
        if (region.End <= prefix)
        {
            return region;
        }

        if (region.Start >= oldEnd)
        {
            return region with { Start = region.Start + delta };
        }

        var start = Math.Min(region.Start, prefix);
        var end = Math.Max(region.End, oldEnd) + delta;

        return new ConflictRegion(start, Math.Max(0, end - start), ConflictPick.Edited);
    }

    private static bool IsMarker(string line) =>
        line.StartsWith(OpeningMarker, StringComparison.Ordinal)
        || line.StartsWith(ClosingMarker, StringComparison.Ordinal)
        || line.TrimEnd() == SeparatorMarker;
}
