using Gitunia.Core.Editing;
using Gitunia.Core.Picker;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>
/// Lays out the repository picker as a framed overlay (title chip, query, hint, grouped rows, notice, chip footer) of
/// styled lines, without a terminal. The lines are the box only: <see cref="PickerLayout"/> says where it sits.
/// </summary>
public static class PickerComposer
{
    private const int MaximumNameColumns = 28;
    private const int MaximumTagColumns = 20;
    private const int MinimumPathColumns = 10;
    private const int ColumnGap = 2;

    /// <param name="picker">The picker state.</param>
    /// <param name="homeDirectory">Paths below it are shown with a leading <c>~</c>.</param>
    /// <param name="width">Columns of the area.</param>
    /// <param name="height">Rows of the area.</param>
    /// <param name="glyphs">The character set for the ellipsis and the frame.</param>
    /// <param name="keys">Where the chip keys come from.</param>
    public static IReadOnlyList<StyledLine> Compose(
        PickerState picker,
        string homeDirectory,
        int width,
        int height,
        GraphGlyphSet glyphs,
        ChipKeys? keys = null)
    {
        if (width <= 0 || height <= 0)
        {
            return [];
        }

        var layout = PickerLayout.Compute(picker, width, height);
        return layout.IsFramed
            ? Framed(picker, homeDirectory, layout, glyphs, keys)
            : Compact(picker, homeDirectory, layout, glyphs);
    }

    private static List<StyledLine> Framed(
        PickerState picker,
        string homeDirectory,
        PickerLayout layout,
        GraphGlyphSet glyphs,
        ChipKeys? keys)
    {
        var width = layout.BoxWidth;
        var inner = layout.InnerWidth;
        var title = picker.LocatingTabId is null ? "Open repository" : "Locate repository";
        var lines = new List<StyledLine>(layout.BoxHeight)
        {
            OverlayBox.Title(title, width, glyphs),
            OverlayBox.Wrap(QueryLine(picker, inner, glyphs), glyphs),
            OverlayBox.Wrap(HintLine(inner, glyphs), glyphs),
        };
        lines.AddRange(ListLines(picker, homeDirectory, layout, glyphs, isFramed: true));
        if (layout.MessageLine >= 0)
        {
            lines.Add(OverlayBox.Text(picker.Message, width, glyphs, SegmentRole.Error));
        }

        lines.Add(OverlayBox.Wrap(Footer(picker, inner, glyphs, keys), glyphs));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return lines;
    }

    private static List<StyledLine> Compact(
        PickerState picker,
        string homeDirectory,
        PickerLayout layout,
        GraphGlyphSet glyphs)
    {
        var lines = new List<StyledLine> { QueryLine(picker, layout.BoxWidth, glyphs) };
        lines.AddRange(ListLines(picker, homeDirectory, layout, glyphs, isFramed: false));
        if (layout.MessageLine >= 0)
        {
            var message = new LineBuilder(layout.BoxWidth, glyphs.Ellipsis).Append($" {picker.Message}", SegmentRole.Error);
            lines.Add(message.Build());
        }

        return [.. lines.Take(layout.BoxHeight)];
    }

    private static StyledLine QueryLine(PickerState picker, int width, GraphGlyphSet glyphs) =>
        new LineBuilder(width, glyphs.Ellipsis)
            .Append(" ", SegmentRole.Plain)
            .Append(PromptCaret.Mark(picker.Query, picker.Caret, Math.Max(1, width - 1)), SegmentRole.Plain)
            .Build();

    private static StyledLine HintLine(int width, GraphGlyphSet glyphs)
    {
        var starts = PathDisplay.PathStartsHint(OperatingSystem.IsWindows());
        return new LineBuilder(width, glyphs.Ellipsis)
            .Append($" type to filter, or a path starting with {starts} (", SegmentRole.Faint)
            .Append("Tab", SegmentRole.OverlayKey)
            .Append(" accepts)", SegmentRole.Faint)
            .Build();
    }

    private static StyledLine Footer(PickerState picker, int inner, GraphGlyphSet glyphs, ChipKeys? keys)
    {
        var buttons = PickerFooterButtons.Layout(picker.HighlightedRow, inner, picker.LocatingTabId is not null, keys);
        var builder = new LineBuilder(inner, glyphs.Ellipsis);
        var column = 0;
        foreach (var button in buttons)
        {
            builder.Append(new string(' ', button.Start - column), SegmentRole.Plain);
            foreach (var segment in KeyedChip.Segments(button, glyphs))
            {
                builder.Append(segment.Text, segment.Role);
            }

            column = button.Start + button.Width;
        }

        return builder.Build();
    }

    private static List<StyledLine> ListLines(
        PickerState picker,
        string homeDirectory,
        PickerLayout layout,
        GraphGlyphSet glyphs,
        bool isFramed)
    {
        var lines = new List<StyledLine>(layout.ListHeight);
        if (layout.ListHeight == 0)
        {
            return lines;
        }

        var width = layout.InnerWidth;
        if (picker.Rows.IsEmpty)
        {
            var empty = new LineBuilder(width, glyphs.Ellipsis).Append(EmptyText(picker), SegmentRole.Faint).Build();
            lines.Add(Wrap(empty, glyphs, isFramed, false));
        }

        var longestName = picker.Rows.Select(row => TextWidth.Of(row.Name)).DefaultIfEmpty(0).Max();
        var nameWidth = Math.Min(Math.Min(MaximumNameColumns, longestName), Math.Max(6, width / 3));
        var longestTags = picker.Rows.Select(row => TagsWidth(Kinds(row))).DefaultIfEmpty(0).Max();
        var tagWidth = Math.Min(MaximumTagColumns, longestTags);
        foreach (var entry in layout.Visible)
        {
            var line = entry.RowIndex < 0
                ? new LineBuilder(width, glyphs.Ellipsis).Append($" {entry.Header}", SegmentRole.SectionTitle).Build()
                : RowLine(picker, entry.RowIndex, homeDirectory, width, nameWidth, tagWidth, glyphs);
            lines.Add(Wrap(line, glyphs, isFramed, entry.RowIndex == picker.Highlight && entry.RowIndex >= 0));
        }

        while (lines.Count < layout.ListHeight)
        {
            lines.Add(Wrap(new LineBuilder(width, glyphs.Ellipsis).Build(), glyphs, isFramed, false));
        }

        return lines;
    }

    private static StyledLine RowLine(
        PickerState picker,
        int index,
        string homeDirectory,
        int width,
        int nameWidth,
        int tagWidth,
        GraphGlyphSet glyphs)
    {
        var row = picker.Rows[index];
        var name = TextWidth.Fit(row.Name, nameWidth, glyphs.Ellipsis);
        var padding = new string(' ', Math.Max(0, nameWidth - TextWidth.Of(name)) + ColumnGap);
        var builder = new LineBuilder(width, glyphs.Ellipsis).Append($" {name}{padding}", SegmentRole.Plain);
        var used = 1 + nameWidth + ColumnGap;
        var kinds = Kinds(row);
        for (var position = 0; position < kinds.Count; position++)
        {
            var text = position == kinds.Count - 1 ? kinds[position].Text : $"{kinds[position].Text} ";
            builder.Append(text, kinds[position].Role);
        }

        var tagsUsed = TagsWidth(kinds);
        builder.Append(new string(' ', Math.Max(0, tagWidth - tagsUsed) + ColumnGap), SegmentRole.Plain);
        used += Math.Max(tagWidth, tagsUsed) + ColumnGap;
        var pathColumns = width - used - 1;
        if (pathColumns >= MinimumPathColumns)
        {
            var path = PathDisplay.Abbreviate(row.Path, homeDirectory, OperatingSystem.IsWindows());
            builder.Append(TextWidth.FitMiddle(path, pathColumns, glyphs.Ellipsis), SegmentRole.Faint);
        }

        return builder.Build(isSelected: index == picker.Highlight);
    }

    private static StyledLine Wrap(StyledLine line, GraphGlyphSet glyphs, bool isFramed, bool isSelected) =>
        isFramed ? OverlayBox.Wrap(line, glyphs, isSelected) : line with { IsSelected = isSelected };

    private static string EmptyText(PickerState picker)
    {
        if (picker.IsListing)
        {
            return picker.IsScanningShown ? " scanning..." : string.Empty;
        }

        if (picker.IsPathMode)
        {
            return " no matching directories";
        }

        if (picker.Query.Length > 0)
        {
            return " no matching repositories";
        }

        return " no repositories yet: type a path to open one";
    }

    private static int TagsWidth(List<PickerKind> kinds) =>
        kinds.Sum(kind => TextWidth.Of(kind.Text)) + Math.Max(0, kinds.Count - 1);

    private static List<PickerKind> Kinds(PickerRow row)
    {
        var kinds = new List<PickerKind>(3);
        if (row.IsOpen)
        {
            kinds.Add(new PickerKind("open", SegmentRole.LocalBranch));
        }

        if (row.IsPinned)
        {
            kinds.Add(new PickerKind("pinned", SegmentRole.CurrentBranch));
        }

        if (row.Tags.HasFlag(PickerTags.Directory))
        {
            var directory = row.Tags.HasFlag(PickerTags.Repository) ? "repository" : "dir";
            kinds.Add(new PickerKind(directory, SegmentRole.Faint));
        }
        else if (!row.IsOpen && !row.IsPinned)
        {
            kinds.Add(RecentOrFound(row));
        }

        if (row.IsMissing)
        {
            kinds.Add(new PickerKind("missing", SegmentRole.FileChanged));
        }

        return kinds;
    }

    private static PickerKind RecentOrFound(PickerRow row)
    {
        if (row.IsRecent)
        {
            return new PickerKind("recent", SegmentRole.RemoteBranch);
        }

        return new PickerKind("found", SegmentRole.Faint);
    }

    private sealed record PickerKind(string Text, SegmentRole Role);
}
