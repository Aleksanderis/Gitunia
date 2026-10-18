using Gitunia.Core.Overlays;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// The lines of a menu or palette list: the entries with a subheader line before each run of one group (#483). A
/// subheader is never an entry, so the selection and the mouse hit-test skip it by construction.
/// </summary>
/// <param name="Lines">Each line: a subheader text, or an entry index into the list.</param>
public sealed record GroupedRows(IReadOnlyList<GroupedRow> Lines)
{
    public static GroupedRows Of(IReadOnlyList<PaletteEntry> entries)
    {
        var lines = new List<GroupedRow>(entries.Count + 4);
        string? previous = null;
        for (var index = 0; index < entries.Count; index++)
        {
            var group = entries[index].Group;
            if (group is not null && group != previous)
            {
                lines.Add(new GroupedRow(group, null));
            }

            previous = group;
            lines.Add(new GroupedRow(null, index));
        }

        return new GroupedRows(lines);
    }

    /// <summary>The slice around <paramref name="selectedEntry"/>: line count, selected line, first line.</summary>
    public MenuWindow Window(int selectedEntry, int maximumRows)
    {
        var rowCount = Math.Max(1, Math.Min(maximumRows, Lines.Count));
        var selectedLine = Lines.Count == 0 ? 0 : Math.Max(0, IndexOfLine(selectedEntry));
        var top = Math.Clamp(selectedLine - (rowCount / 2), 0, Math.Max(0, Lines.Count - rowCount));
        return new MenuWindow(rowCount, selectedLine, top);
    }

    /// <summary>The entry on the list line <paramref name="line"/>, null for a subheader or past the end.</summary>
    public int? EntryAt(int line) => line >= 0 && line < Lines.Count ? Lines[line].Entry : null;

    private int IndexOfLine(int entry)
    {
        for (var line = 0; line < Lines.Count; line++)
        {
            if (Lines[line].Entry == entry)
            {
                return line;
            }
        }

        return -1;
    }
}

/// <summary>One line of a grouped list.</summary>
/// <param name="Header">The subheader text, null for an entry line.</param>
/// <param name="Entry">The entry index, null for a subheader line.</param>
public readonly record struct GroupedRow(string? Header, int? Entry);
