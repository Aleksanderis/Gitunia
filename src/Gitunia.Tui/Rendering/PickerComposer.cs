using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;

namespace Gitunia.Tui.Rendering;

/// <summary>Lays out the repo picker (prompt, hint, rows, notice) as styled lines of an exact size, without a terminal.</summary>
public static class PickerComposer
{
    /// <summary>Rows above the list: the prompt and the hint.</summary>
    public const int HeaderRows = 2;

    private const int MaximumNameColumns = 28;
    private const int TagColumns = 16;

    /// <param name="picker">The picker state.</param>
    /// <param name="homeDirectory">Paths below it are shown with a leading <c>~</c>.</param>
    /// <param name="width">Columns.</param>
    /// <param name="height">Rows available for the whole picker.</param>
    /// <param name="glyphs">The character set for the ellipsis and the cursor.</param>
    public static IReadOnlyList<StyledLine> Compose(PickerState picker, string homeDirectory, int width, int height, GraphGlyphSet glyphs)
    {
        var lines = new List<StyledLine>(height);
        if (height <= 0)
        {
            return lines;
        }

        var title = picker.LocatingTabId is null ? "Open repository" : "Locate repository";
        lines.Add(new LineBuilder(width, glyphs.Ellipsis)
            .Append(" " + title + ": ", SegmentRole.Faint)
            .Append(picker.Query, SegmentRole.Plain)
            .Append("_", SegmentRole.Faint)
            .Build());
        if (height > 1)
        {
            lines.Add(new LineBuilder(width, glyphs.Ellipsis)
                .Append(" type to filter, or a path starting with / ~/ ./ ../ (Tab completes)", SegmentRole.Faint)
                .Build());
        }

        var hasMessage = picker.Message.Length > 0;
        var listHeight = Math.Max(0, height - HeaderRows - (hasMessage ? 1 : 0));
        lines.AddRange(ListLines(picker, homeDirectory, width, listHeight, glyphs));
        if (hasMessage && lines.Count < height)
        {
            lines.Add(new LineBuilder(width, glyphs.Ellipsis).Append(" " + picker.Message, SegmentRole.Error).Build());
        }

        while (lines.Count < height)
        {
            lines.Add(new LineBuilder(width, glyphs.Ellipsis).Build());
        }

        return lines.Take(height).ToList();
    }

    /// <summary>The first list row shown for a highlight, so the highlight stays visible.</summary>
    public static int TopRow(int highlight, int listHeight) => highlight < listHeight ? 0 : highlight - listHeight + 1;

    private static List<StyledLine> ListLines(PickerState picker, string homeDirectory, int width, int listHeight, GraphGlyphSet glyphs)
    {
        var lines = new List<StyledLine>(listHeight);
        if (listHeight == 0)
        {
            return lines;
        }

        if (picker.Rows.IsEmpty)
        {
            var empty = picker.IsListing ? (picker.IsScanningShown ? " scanning..." : string.Empty) : picker.IsPathMode ? " no matching directories" : picker.Query.Length > 0 ? " no matching repositories" : " no repositories yet: type a path to open one";
            lines.Add(new LineBuilder(width, glyphs.Ellipsis).Append(empty, SegmentRole.Faint).Build());
            return lines;
        }

        var nameWidth = Math.Min(MaximumNameColumns, picker.Rows.Max(row => TextWidth.Of(row.Name)));
        var top = TopRow(picker.Highlight, listHeight);
        for (var index = top; index < picker.Rows.Length && lines.Count < listHeight; index++)
        {
            var row = picker.Rows[index];
            var name = TextWidth.Fit(row.Name, nameWidth, glyphs.Ellipsis);
            var builder = new LineBuilder(width, glyphs.Ellipsis)
                .Append(" " + name + new string(' ', Math.Max(0, nameWidth - TextWidth.Of(name))) + "  ", SegmentRole.Plain)
                .Append(Tags(row).PadRight(TagColumns), SegmentRole.Tag)
                .Append(Abbreviate(row.Path, homeDirectory), SegmentRole.Faint);
            lines.Add(builder.Build(SegmentRole.Plain, isSelected: index == picker.Highlight));
        }

        return lines;
    }

    private static string Tags(PickerRow row)
    {
        var tags = new List<string>(3);
        if (row.IsOpen)
        {
            tags.Add("open");
        }

        if (row.IsPinned)
        {
            tags.Add("pinned");
        }

        if (row.Tags.HasFlag(PickerTags.Directory))
        {
            tags.Add(row.Tags.HasFlag(PickerTags.Repository) ? "repo" : "dir");
        }
        else if (!row.IsOpen && !row.IsPinned)
        {
            tags.Add(row.IsRecent ? "recent" : "found");
        }

        return string.Join(' ', tags);
    }

    private static string Abbreviate(string path, string homeDirectory)
    {
        if (homeDirectory.Length == 0)
        {
            return path;
        }

        var home = Path.TrimEndingDirectorySeparator(homeDirectory);
        if (RepositoryPathComparer.Instance.Equals(path, home))
        {
            return "~";
        }

        var prefix = home + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return path.StartsWith(prefix, comparison) ? "~" + Path.DirectorySeparatorChar + path[prefix.Length..] : path;
    }
}
