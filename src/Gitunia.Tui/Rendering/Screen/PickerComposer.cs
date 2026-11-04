using System.Globalization;
using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Picker;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>
/// Lays out the repository picker as a framed overlay (title bar with the count, query line, grouped rows with a glyph
/// column, notice, rule and chip footer) of styled lines, without a terminal. The lines are the box only:
/// <see cref="PickerLayout"/> says where it sits.
/// </summary>
public static class PickerComposer
{
    private const int MaximumNameColumns = 24;
    private const int MaximumBranchColumns = 20;
    private const int MinimumPathColumns = 10;
    private const int ColumnGap = 2;
    private const int GlyphGap = 2;
    private const int MinimumTitleGap = 2;
    private const string QueryPlaceholder = "type to filter, or a path";
    private const string AcceptLabel = " accepts a path";

    /// <param name="picker">The picker state.</param>
    /// <param name="homeDirectory">Paths below it are shown with a leading <c>~</c>.</param>
    /// <param name="width">Columns of the area.</param>
    /// <param name="height">Rows of the area.</param>
    /// <param name="glyphs">The character set for the ellipsis, the frame, the rules and the row marks.</param>
    /// <param name="keys">Where the chip keys come from.</param>
    /// <param name="openBranches">The checked-out branch of each open tab by repository path; only open rows use it, so
    /// no process is started for any other row. Null or empty leaves the branch column out.</param>
    public static IReadOnlyList<StyledLine> Compose(
        PickerState picker,
        string homeDirectory,
        int width,
        int height,
        GraphGlyphSet glyphs,
        ChipKeys? keys = null,
        IReadOnlyDictionary<string, string>? openBranches = null)
    {
        if (width <= 0 || height <= 0)
        {
            return [];
        }

        var layout = PickerLayout.Compute(picker, width, height);
        return layout.IsFramed
            ? Framed(picker, homeDirectory, layout, glyphs, keys, openBranches)
            : Compact(picker, homeDirectory, layout, glyphs, keys, openBranches);
    }

    private static List<StyledLine> Framed(
        PickerState picker,
        string homeDirectory,
        PickerLayout layout,
        GraphGlyphSet glyphs,
        ChipKeys? keys,
        IReadOnlyDictionary<string, string>? openBranches)
    {
        var width = layout.BoxWidth;
        var inner = layout.InnerWidth;
        var lines = new List<StyledLine>(layout.BoxHeight)
        {
            TitleLine(picker, width, glyphs),
            OverlayBox.Wrap(QueryLine(picker, inner, glyphs, true, keys), glyphs),
            OverlayBox.Wrap(new LineBuilder(inner, glyphs.Ellipsis).Build(), glyphs),
        };
        lines.AddRange(ListLines(picker, homeDirectory, layout, glyphs, true, openBranches));
        if (layout.MessageLine >= 0)
        {
            lines.Add(OverlayBox.Text(picker.Message, width, glyphs, SegmentRole.Error));
        }

        lines.Add(OverlayBox.Wrap(Rule(inner, glyphs, 1), glyphs));
        lines.Add(OverlayBox.Wrap(Footer(picker, inner, glyphs, keys, layout), glyphs));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return lines;
    }

    private static List<StyledLine> Compact(
        PickerState picker,
        string homeDirectory,
        PickerLayout layout,
        GraphGlyphSet glyphs,
        ChipKeys? keys,
        IReadOnlyDictionary<string, string>? openBranches)
    {
        var lines = new List<StyledLine> { QueryLine(picker, layout.BoxWidth, glyphs, false, keys) };
        lines.AddRange(ListLines(picker, homeDirectory, layout, glyphs, false, openBranches));
        if (layout.MessageLine >= 0)
        {
            var message = new LineBuilder(layout.BoxWidth, glyphs.Ellipsis).Append($" {picker.Message}", SegmentRole.Error);
            lines.Add(message.Build());
        }

        return [.. lines.Take(layout.BoxHeight)];
    }

    private static StyledLine TitleLine(PickerState picker, int width, GraphGlyphSet glyphs)
    {
        var title = picker.LocatingTabId is null ? "Open repository" : "Locate repository";
        var left = $" {title}";
        var count = $"{CountText(picker)} ";
        var builder = new LineBuilder(width, glyphs.Ellipsis).Append(left, SegmentRole.OverlayTitle);
        var gap = width - TextWidth.Of(left) - TextWidth.Of(count);
        if (gap >= MinimumTitleGap)
        {
            builder.Append(new string(' ', gap), SegmentRole.OverlayTitle).Append(count, SegmentRole.OverlayTitle);
        }

        return builder.Build(SegmentRole.OverlayTitle);
    }

    private static string CountText(PickerState picker)
    {
        var count = picker.Rows.Length.ToString(CultureInfo.InvariantCulture);
        if (picker.IsPathMode)
        {
            return picker.Rows.Length == 1 ? $"{count} directory" : $"{count} directories";
        }

        return picker.Rows.Length == 1 ? $"{count} repository" : $"{count} repositories";
    }

    private static StyledLine QueryLine(PickerState picker, int width, GraphGlyphSet glyphs, bool withHint, ChipKeys? keys)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis)
            .Append($" {glyphs.Picker.Prompt} ", SegmentRole.SectionTitle)
            .Append(PromptCaret.Mark(picker.Query, picker.Caret, Math.Max(1, width - 3)), SegmentRole.Plain);
        if (picker.Query.Length == 0)
        {
            builder.Append($" {QueryPlaceholder}", SegmentRole.Faint);
        }

        var used = width - builder.Remaining;
        var acceptKey = (keys ?? ChipKeys.Default).ForAction(ActionIds.PickerCompletePath, DefaultKeymap.PickerView, KeyMode.Insert);
        if (!withHint || acceptKey is null)
        {
            return builder.Build();
        }

        var hintWidth = TextWidth.Of(acceptKey) + TextWidth.Of(AcceptLabel);
        if (used + ColumnGap + hintWidth + 1 <= width)
        {
            builder
                .Append(new string(' ', width - used - hintWidth - 1), SegmentRole.Plain)
                .Append(acceptKey, ChipStyle.Pane.Key)
                .Append(AcceptLabel, SegmentRole.Faint);
        }

        return builder.Build();
    }

    // A rule of the faint role with one column of room on the left and on the right.
    private static StyledLine Rule(int width, GraphGlyphSet glyphs, int offset)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis).Append(new string(' ', offset), SegmentRole.Plain);
        builder.Append(new string(glyphs.Horizontal, Math.Max(0, builder.Remaining - 1)), SegmentRole.Faint);
        return builder.Build();
    }

    private static StyledLine Footer(
        PickerState picker,
        int inner,
        GraphGlyphSet glyphs,
        ChipKeys? keys,
        PickerLayout layout)
    {
        var buttons = PickerFooterButtons.Layout(
            picker.HighlightedRow,
            inner,
            picker.LocatingTabId is not null,
            keys,
            PickerFooterButtons.AcceptLabel(picker, layout.Visible));
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
        bool isFramed,
        IReadOnlyDictionary<string, string>? openBranches)
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

        var longestName = picker.Rows.Select(row => TextWidth.Of(DisplayName(row))).DefaultIfEmpty(0).Max();
        var nameWidth = Math.Min(Math.Min(MaximumNameColumns, longestName), Math.Max(6, width / 3));
        var branchWidth = BranchWidth(picker, openBranches, homeDirectory, width, nameWidth, glyphs);
        var selected = PickerListPlan.PositionOf(layout.Visible, picker);
        for (var offset = 0; offset < layout.Visible.Count; offset++)
        {
            var isSelected = offset == selected;
            var line = ListLine(
                picker,
                layout.Visible[offset],
                isSelected,
                homeDirectory,
                width,
                nameWidth,
                branchWidth,
                openBranches,
                glyphs);
            lines.Add(Wrap(line, glyphs, isFramed, isSelected));
        }

        while (lines.Count < layout.ListHeight)
        {
            lines.Add(Wrap(new LineBuilder(width, glyphs.Ellipsis).Build(), glyphs, isFramed, false));
        }

        return lines;
    }

    private static StyledLine ListLine(
        PickerState picker,
        PickerListEntry entry,
        bool isSelected,
        string homeDirectory,
        int width,
        int nameWidth,
        int branchWidth,
        IReadOnlyDictionary<string, string>? openBranches,
        GraphGlyphSet glyphs)
    {
        return entry.Kind switch
        {
            PickerEntryKind.Spacer => new LineBuilder(width, glyphs.Ellipsis).Build(),
            PickerEntryKind.Header => HeaderLine(entry, width, glyphs),
            PickerEntryKind.Row => RowLine(
                picker,
                entry.RowIndex,
                isSelected,
                homeDirectory,
                width,
                nameWidth,
                branchWidth,
                openBranches,
                glyphs),
            _ => ToggleLine(entry, isSelected, width, glyphs),
        };
    }

    // The group name in capitals, the shown count when the section is capped, then a rule to the right edge.
    private static StyledLine HeaderLine(PickerListEntry entry, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis)
            .Append($" {SectionName(entry.Section)}", SegmentRole.SectionTitle);
        if (entry.Shown < entry.Total)
        {
            var shown = entry.Shown.ToString(CultureInfo.InvariantCulture);
            var total = entry.Total.ToString(CultureInfo.InvariantCulture);
            builder.Append($"  {shown} of {total}", SegmentRole.Faint);
        }

        builder.Append(" ", SegmentRole.Plain);
        builder.Append(new string(glyphs.Horizontal, Math.Max(0, builder.Remaining - 1)), SegmentRole.Faint);
        return builder.Build();
    }

    private static string SectionName(PickerSection section) => section switch
    {
        PickerSection.Open => "OPEN",
        PickerSection.Pinned => "PINNED",
        PickerSection.Recent => "RECENT",
        _ => "FOUND",
    };

    // The line that expands a capped section ("N more") or collapses an expanded one, under the rows' glyph column.
    private static StyledLine ToggleLine(PickerListEntry entry, bool isSelected, int width, GraphGlyphSet glyphs)
    {
        var isMore = entry.Kind == PickerEntryKind.More;
        var mark = isMore ? glyphs.Picker.More : glyphs.Picker.Less;
        var text = isMore ? $"{entry.Hidden.ToString(CultureInfo.InvariantCulture)} more" : "less";
        return new LineBuilder(width, glyphs.Ellipsis)
            .Append(" ", SegmentRole.Plain)
            .Append(mark, SegmentRole.Faint)
            .Append(new string(' ', GlyphGap), SegmentRole.Plain)
            .Append(text, SegmentRole.Faint)
            .Build(isSelected: isSelected);
    }

    // Glyph, name, then the path right-aligned; a second mark for a pinned row whose glyph says something else sits
    // after the path, in a column every row keeps free so the paths line up.
    private static StyledLine RowLine(
        PickerState picker,
        int index,
        bool isSelected,
        string homeDirectory,
        int width,
        int nameWidth,
        int branchWidth,
        IReadOnlyDictionary<string, string>? openBranches,
        GraphGlyphSet glyphs)
    {
        var row = picker.Rows[index];
        var mark = MarkOf(row, glyphs.Picker);
        var name = TextWidth.Fit(DisplayName(row), nameWidth, glyphs.Ellipsis);
        var padding = new string(' ', Math.Max(0, nameWidth - TextWidth.Of(name)) + ColumnGap);
        var builder = new LineBuilder(width, glyphs.Ellipsis)
            .Append(" ", SegmentRole.Plain)
            .Append(mark.Text, mark.Role)
            .Append(new string(' ', GlyphGap), SegmentRole.Plain)
            .Append($"{name}{padding}", SegmentRole.Plain);
        if (branchWidth > 0)
        {
            var branch = BranchOf(row, openBranches) is { } text ? TextWidth.Fit(text, branchWidth, glyphs.Ellipsis) : string.Empty;
            builder.Append(branch, SegmentRole.CurrentBranch);
            builder.Append(new string(' ', branchWidth - TextWidth.Of(branch) + ColumnGap), SegmentRole.Plain);
        }

        var pathColumns = builder.Remaining - 1 - TextWidth.Of(glyphs.Picker.Pinned) - 1;
        if (pathColumns >= MinimumPathColumns)
        {
            var path = PathDisplay.Abbreviate(row.Path, homeDirectory, OperatingSystem.IsWindows());
            var fitted = TextWidth.FitMiddle(path, pathColumns, glyphs.Ellipsis);
            builder.Append(new string(' ', pathColumns - TextWidth.Of(fitted)), SegmentRole.Plain);
            builder.Append(fitted, SegmentRole.Faint);
            var isStarred = row.IsPinned && mark.Text != glyphs.Picker.Pinned;
            builder.Append(" ", SegmentRole.Plain);
            builder.Append(isStarred ? glyphs.Picker.Pinned : " ", SegmentRole.CurrentBranch);
        }

        return builder.Build(isSelected: isSelected);
    }

    // The branch of an open, present repository; detached or unloaded tabs have none.
    private static string? BranchOf(PickerRow row, IReadOnlyDictionary<string, string>? openBranches) =>
        row.IsOpen && !row.IsMissing && openBranches is not null && openBranches.TryGetValue(row.Path, out var branch)
            && branch.Length > 0
            ? branch
            : null;

    // The branch column's width (0 when no row has a branch, or when keeping it would shorten the longest path).
    private static int BranchWidth(
        PickerState picker,
        IReadOnlyDictionary<string, string>? openBranches,
        string homeDirectory,
        int width,
        int nameWidth,
        GraphGlyphSet glyphs)
    {
        var longestBranch = picker.Rows
            .Select(row => BranchOf(row, openBranches))
            .Where(branch => branch is not null)
            .Select(branch => TextWidth.Of(branch!))
            .DefaultIfEmpty(0)
            .Max();
        if (longestBranch == 0)
        {
            return 0;
        }

        var branchWidth = Math.Min(MaximumBranchColumns, longestBranch);
        var longestPath = picker.Rows
            .Select(row => TextWidth.Of(PathDisplay.Abbreviate(row.Path, homeDirectory, OperatingSystem.IsWindows())))
            .Max();
        var fixedColumns = 1 + TextWidth.Of(glyphs.Picker.Open) + GlyphGap + nameWidth + ColumnGap;
        var pathRoom = width - fixedColumns - 1 - TextWidth.Of(glyphs.Picker.Pinned) - 1;
        return pathRoom - branchWidth - ColumnGap >= Math.Max(longestPath, MinimumPathColumns) ? branchWidth : 0;
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

    // A directory that is not a repository ends in the path separator, so it reads as a folder without colour.
    private static string DisplayName(PickerRow row)
    {
        var isFolder = row.Tags.HasFlag(PickerTags.Directory) && !row.Tags.HasFlag(PickerTags.Repository);
        return isFolder ? $"{row.Name}{Path.DirectorySeparatorChar}" : row.Name;
    }

    // One mark per row, the first that applies: missing, open, pinned, recent, else found (or a directory).
    private static PickerMark MarkOf(PickerRow row, PickerGlyphs marks)
    {
        if (row.IsMissing)
        {
            return new PickerMark(marks.Missing, SegmentRole.FileChanged);
        }

        if (row.IsOpen)
        {
            return new PickerMark(marks.Open, SegmentRole.LocalBranch);
        }

        if (row.IsPinned)
        {
            return new PickerMark(marks.Pinned, SegmentRole.CurrentBranch);
        }

        if (row.IsRecent && !row.Tags.HasFlag(PickerTags.Directory))
        {
            return new PickerMark(marks.Recent, SegmentRole.RemoteBranch);
        }

        return new PickerMark(marks.Found, SegmentRole.Faint);
    }

    private sealed record PickerMark(string Text, SegmentRole Role);
}
