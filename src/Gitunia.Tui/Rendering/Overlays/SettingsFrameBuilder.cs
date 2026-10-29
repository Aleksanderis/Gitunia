using System.Globalization;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out the settings overlay (ux.md, "Settings screen"): the categories on the left, the settings of the selected
/// category on the right, a help line under the lists and a hint row. Under 80 columns the categories collapse into
/// one list with headings; under 40x12 only a "terminal too small" line is shown. Also finds what a click landed on.
/// </summary>
public static class SettingsFrameBuilder
{
    private const string Title = "Settings";
    private const string ReadOnlyTitle = "Settings (read-only)";
    private const string FilterPlaceholder = "/ filter";

    private const string Cursor = "_";
    private const string TooSmallText = "Terminal too small for the settings";
    private const string TooSmallHint = "Needs 40x12. Esc closes.";
    private const string AnswerYes = "y  remove it";
    private const string AnswerNo = "n  keep it";
    private const string CategoryHelp = "Enter or l opens the category; j and k move between categories.";
    private const int MaximumHelpLines = 2;


    private static readonly HintItem Move = new("j/k", "move");
    private static readonly HintItem Back = new("h", "back");
    private static readonly HintItem Filter = new("/", "filter");
    private static readonly HintItem Close = new("Esc", "close");

    /// <summary>The settings frame for a screen, or null when the screen is too small to show even a title.</summary>
    /// <param name="state">The settings state.</param>
    /// <param name="screenWidth">Terminal columns.</param>
    /// <param name="screenHeight">Terminal rows.</param>
    /// <param name="glyphs">The character set.</param>
    public static OverlayFrame? Build(SettingsState state, int screenWidth, int screenHeight, GraphGlyphSet glyphs)
    {
        if (SettingsLayout.For(screenWidth, screenHeight) is not { } layout)
        {
            return null;
        }

        var left = (screenWidth - layout.Width) / 2;
        var title = state.IsReadOnly ? ReadOnlyTitle : Title;
        var lines = new List<StyledLine> { OverlayBox.Title(title, layout.Width, glyphs) };
        if (layout.IsTooSmall)
        {
            lines.Add(OverlayBox.Text(TooSmallText, layout.Width, glyphs, SegmentRole.Error));
            lines.Add(OverlayBox.Text(TooSmallHint, layout.Width, glyphs, SegmentRole.Faint));
            lines.Add(OverlayBox.Bottom(layout.Width, glyphs));

            return new OverlayFrame(left, 1, lines);
        }

        lines.Add(FilterLine(state, layout, glyphs));
        lines.AddRange(BodyLines(state, layout, glyphs));
        lines.Add(RuleLine(layout, glyphs));
        lines.AddRange(HelpLines(state, layout, glyphs));
        lines.Add(OverlayBox.Hints(HintText(state), layout.Width, glyphs));
        lines.Add(OverlayBox.Bottom(layout.Width, glyphs));

        return new OverlayFrame(left, 1, lines);
    }

    /// <summary>The rows the lists show at a screen size (the page of the half-page motions); 0 when none.</summary>
    /// <param name="screenWidth">Terminal columns.</param>
    /// <param name="screenHeight">Terminal rows.</param>
    public static int VisibleRows(int screenWidth, int screenHeight) =>
        SettingsLayout.For(screenWidth, screenHeight) is { IsTooSmall: false } layout ? layout.BodyRows : 0;

    /// <summary>The lines of the settings list: rows, plus a heading before each category when collapsed.</summary>
    /// <param name="state">The settings state.</param>
    public static IReadOnlyList<SettingsBodyEntry> BodyEntries(SettingsState state)
    {
        var rows = state.Rows;
        var entries = new List<SettingsBodyEntry>();
        SettingCategory? previous = null;
        var previousSection = string.Empty;
        for (var index = 0; index < rows.Count; index++)
        {
            var category = rows[index].Definition.Category;
            if (state.IsCollapsed && !state.IsFiltering && category != previous)
            {
                entries.Add(new SettingsBodyEntry(-1, SettingsRegistry.TitleOf(category)));
            }

            previous = category;
            if (rows[index].Kind == SettingRowKind.Binding
                && state.Reference[rows[index].Index].Section != previousSection)
            {
                previousSection = state.Reference[rows[index].Index].Section;
                entries.Add(new SettingsBodyEntry(-1, previousSection));
            }

            entries.Add(new SettingsBodyEntry(index, string.Empty));
        }

        return entries;
    }

    /// <summary>Finds what a click landed on.</summary>
    /// <param name="state">The settings state.</param>
    /// <param name="column">The column of the click inside the frame (0 is the left border).</param>
    /// <param name="line">The line of the click inside the frame (0 is the title).</param>
    /// <param name="screenWidth">Terminal columns.</param>
    /// <param name="screenHeight">Terminal rows.</param>
    /// <param name="glyphs">The character set (the width of a control depends on it).</param>
    public static SettingsHit HitAt(
        SettingsState state,
        int column,
        int line,
        int screenWidth,
        int screenHeight,
        GraphGlyphSet glyphs)
    {
        if (SettingsLayout.For(screenWidth, screenHeight) is not { IsTooSmall: false } layout)
        {
            return SettingsHit.None;
        }

        var inner = column - 1;
        if (inner < 0 || inner >= layout.Width - 2)
        {
            return SettingsHit.None;
        }

        if (state.Removal is not null && line == layout.SecondHelpLine)
        {
            var isYes = inner < (layout.Width - OverlayBox.BorderWidth) / 2;

            return new SettingsHit(isYes ? SettingsHitKind.ConfirmYes : SettingsHitKind.ConfirmNo);
        }

        var offset = line - SettingsLayout.BodyStart;
        if (offset < 0 || offset >= layout.BodyRows)
        {
            return SettingsHit.None;
        }

        if (!layout.IsCollapsed && inner < layout.CategoryWidth)
        {
            return CategoryHit(state, layout, offset);
        }

        var start = layout.IsCollapsed ? 0 : layout.CategoryWidth + 1;

        return inner < start ? SettingsHit.None : RowHit(state, layout, offset, inner - start, glyphs);
    }

    private static SettingsHit CategoryHit(SettingsState state, SettingsLayout layout, int offset)
    {
        var count = state.Categories.Count;
        var index = Top(count, state.CategoryIndex, layout.BodyRows) + offset;

        return index < count ? new SettingsHit(SettingsHitKind.Category, index) : SettingsHit.None;
    }

    private static SettingsHit RowHit(
        SettingsState state,
        SettingsLayout layout,
        int offset,
        int x,
        GraphGlyphSet glyphs)
    {
        var entries = BodyEntries(state);
        var index = EntriesTop(state, entries, layout) + offset;
        if (index >= entries.Count || entries[index].RowIndex < 0)
        {
            return SettingsHit.None;
        }

        var rowIndex = entries[index].RowIndex;
        var row = state.Rows[rowIndex];
        var right = SettingsRowText.FitRight(
            SettingsRowText.Of(state, row, glyphs).Right,
            layout.SettingsWidth,
            glyphs.Ellipsis);
        var rightWidth = TextWidth.Of(right);
        var rightStart = layout.SettingsWidth - rightWidth - 1;
        if (rightWidth == 0 || x < rightStart)
        {
            return new SettingsHit(SettingsHitKind.Row, rowIndex);
        }

        if (row.Kind == SettingRowKind.Item)
        {
            return new SettingsHit(SettingsHitKind.Remove, rowIndex);
        }

        var marker = SettingsRowText.AddMarker.Length;
        var isAdd = row.Definition.Control == SettingControl.PathList && x >= layout.SettingsWidth - 1 - marker;

        return new SettingsHit(isAdd ? SettingsHitKind.Add : SettingsHitKind.Control, rowIndex);
    }

    private static StyledLine FilterLine(SettingsState state, SettingsLayout layout, GraphGlyphSet glyphs)
    {
        if (state.IsEditingFilter)
        {
            return OverlayBox.Text($"/{state.Filter}{Cursor}", layout.Width, glyphs);
        }

        if (state.IsReadOnly)
        {
            return OverlayBox.Text(state.Banner, layout.Width, glyphs, SegmentRole.Error);
        }

        if (state.IsFiltering)
        {
            var count = state.Rows.Count(
                row => row.Definition.Control != SettingControl.KeyReference || row.Kind == SettingRowKind.Binding);
            var noun = count == 1 ? "match" : "matches";
            var text = string.Create(CultureInfo.InvariantCulture, $"/{state.Filter}  {count} {noun}");

            return OverlayBox.Text(text, layout.Width, glyphs);
        }

        return OverlayBox.Text(FilterPlaceholder, layout.Width, glyphs, SegmentRole.Faint);
    }

    private static List<StyledLine> BodyLines(SettingsState state, SettingsLayout layout, GraphGlyphSet glyphs)
    {
        var lines = new List<StyledLine>();
        var entries = BodyEntries(state);
        var categories = state.Categories;
        var categoryTop = Top(categories.Count, state.CategoryIndex, layout.BodyRows);
        var entryTop = EntriesTop(state, entries, layout);
        var side = new StyledSegment(
            glyphs.For(LineDirections.Up | LineDirections.Down).ToString(),
            SegmentRole.FocusedBorder,
            Selection: SegmentSelection.None);
        var divider = side with { Role = SegmentRole.Border };
        for (var offset = 0; offset < layout.BodyRows; offset++)
        {
            var segments = new List<StyledSegment> { side };
            if (!layout.IsCollapsed)
            {
                var index = categoryTop + offset;
                segments.AddRange(CategoryCell(state, categories, index, layout.CategoryWidth, glyphs));
                segments.Add(divider);
            }

            var entry = entryTop + offset < entries.Count ? entries[entryTop + offset] : null;
            segments.AddRange(SettingsCell(state, entry, layout.SettingsWidth, glyphs));
            segments.Add(side);
            lines.Add(new StyledLine(segments));
        }

        return lines;
    }

    private static IReadOnlyList<StyledSegment> CategoryCell(
        SettingsState state,
        IReadOnlyList<SettingCategory> categories,
        int index,
        int width,
        GraphGlyphSet glyphs)
    {
        if (index >= categories.Count)
        {
            return Cell(" ", string.Empty, string.Empty, width, SegmentSelection.None, glyphs, SegmentRole.Plain);
        }

        var isCurrent = index == state.CategoryIndex;
        var selection = isCurrent ? ColumnSelection(state, SettingsColumn.Categories) : SegmentSelection.None;

        var title = SettingsRegistry.TitleOf(categories[index]);

        return Cell(" ", title, string.Empty, width, selection, glyphs, SegmentRole.Plain);
    }

    private static IReadOnlyList<StyledSegment> SettingsCell(
        SettingsState state,
        SettingsBodyEntry? entry,
        int width,
        GraphGlyphSet glyphs)
    {
        if (entry is null)
        {
            return Cell(" ", string.Empty, string.Empty, width, SegmentSelection.None, glyphs, SegmentRole.Plain);
        }

        if (entry.RowIndex < 0)
        {
            return Cell(
                " ",
                entry.Heading,
                string.Empty,
                width,
                SegmentSelection.None,
                glyphs,
                SegmentRole.SectionTitle);
        }

        var row = state.Rows[entry.RowIndex];
        var text = SettingsRowText.Of(state, row, glyphs);
        var isCurrent = entry.RowIndex == Math.Clamp(state.Cursor, 0, Math.Max(0, state.Rows.Count - 1));
        var showsCursor = state.Column == SettingsColumn.Settings || state.ListsAllCategories;
        var selection = SegmentSelection.None;
        if (isCurrent && showsCursor)
        {
            selection = ColumnSelection(state, SettingsColumn.Settings);
        }

        return Cell(text.Marker, text.Left, text.Right, width, selection, glyphs, SegmentRole.Plain);
    }

    private static SegmentSelection ColumnSelection(SettingsState state, SettingsColumn column)
    {
        var focused = state.Column == column || state.ListsAllCategories && column == SettingsColumn.Settings;

        return focused ? SegmentSelection.Focused : SegmentSelection.Dim;
    }

    // One cell of exactly the given width: the marker, the label, the control pushed to the right edge.
    private static IReadOnlyList<StyledSegment> Cell(
        string marker,
        string left,
        string right,
        int width,
        SegmentSelection selection,
        GraphGlyphSet glyphs,
        SegmentRole leftRole)
    {
        var rightFitted = SettingsRowText.FitRight(right, width, glyphs.Ellipsis);
        var rightWidth = TextWidth.Of(rightFitted);
        var trailing = rightWidth > 0 ? " " : string.Empty;
        var room = Math.Max(0, width - 1 - rightWidth - trailing.Length - (rightWidth > 0 ? 1 : 0));
        var leftFitted = TextWidth.Fit($" {left}", room, glyphs.Ellipsis);
        var gap = Math.Max(0, width - 1 - TextWidth.Of(leftFitted) - rightWidth - trailing.Length);
        var markerRole = marker == SettingsRowText.Warning ? SegmentRole.Error : leftRole;
        var line = new LineBuilder(width, glyphs.Ellipsis)
            .Append(marker, markerRole)
            .Append(leftFitted, leftRole)
            .Append(new string(' ', gap), leftRole)
            .Append(rightFitted, SegmentRole.Plain)
            .Append(trailing, SegmentRole.Plain)
            .Build(leftRole);

        return [.. line.Segments.Select(segment => segment with { Selection = selection })];
    }

    private static StyledLine RuleLine(SettingsLayout layout, GraphGlyphSet glyphs) =>
        OverlayBox.Text(
            new string(glyphs.Horizontal, layout.Width - OverlayBox.BorderWidth - 2),
            layout.Width,
            glyphs,
            SegmentRole.Faint);

    private static List<StyledLine> HelpLines(SettingsState state, SettingsLayout layout, GraphGlyphSet glyphs)
    {
        if (state.Removal is { } removal)
        {
            var setting = state.Definitions.FirstOrDefault(definition => definition.Id == removal.SettingId);
            var name = setting?.Name ?? string.Empty;
            var question = $"Remove \"{removal.Path}\" from {name}? The folder itself is not touched.";

            return
            [
                OverlayBox.Text(question, layout.Width, glyphs),
                OverlayBox.TwoColumns(
                    AnswerYes,
                    AnswerNo,
                    layout.Width,
                    glyphs,
                    SegmentRole.OverlayKey,
                    SegmentRole.OverlayKey),
            ];
        }

        var texts = HelpTexts(state, layout);

        return
        [
            OverlayBox.Text(texts[0], layout.Width, glyphs, SegmentRole.Faint),
            OverlayBox.Text(texts[1], layout.Width, glyphs, SegmentRole.Faint),
        ];
    }

    // Two lines: the warning (if the file holds a bad value), what the setting does and where it is written.
    private static string[] HelpTexts(SettingsState state, SettingsLayout layout)
    {
        var width = layout.Width - OverlayBox.BorderWidth - 1;
        if (state.Column == SettingsColumn.Categories && !state.ListsAllCategories)
        {
            return [CategoryHelp, string.Empty];
        }

        if (state.Selected is not { } row)
        {
            return [state.IsFiltering ? "Nothing matches the filter." : "Nothing to show.", string.Empty];
        }

        var definition = row.Definition;
        if (row.Kind == SettingRowKind.Binding)
        {
            var entry = state.Reference[row.Index];
            var text = $"Action {entry.ActionId}. Enter shows the keys entry of config.json that binds it again.";
            var lines = HelpFrameBuilder.WrapText(text, width);

            return [lines.FirstOrDefault() ?? string.Empty, lines.ElementAtOrDefault(1) ?? string.Empty];
        }

        var warning = string.Empty;
        if (state.Warnings.TryGetValue(definition.Id, out var reason))
        {
            warning = $"! {reason}. Changing it writes a valid value. ";
        }

        var wrapped = HelpFrameBuilder.WrapText($"{warning}{definition.Help} {definition.Detail}".Trim(), width);
        var second = wrapped.ElementAtOrDefault(1) ?? string.Empty;
        if (wrapped.Count > MaximumHelpLines)
        {
            second = string.Join(' ', wrapped.Skip(1));
        }

        return [wrapped.FirstOrDefault() ?? string.Empty, second];
    }

    private static HintItem[] HintText(SettingsState state)
    {
        if (state.Removal is not null)
        {
            return [new("y", "remove"), new("n", "keep"), new("Esc", "keep")];
        }

        if (state.Input is not null)
        {
            return [new("Enter", "save"), new("Esc", "cancel")];
        }

        if (state.IsEditingFilter)
        {
            return [new(string.Empty, "type to filter"), new("Enter", "keep"), new("Esc", "clear")];
        }

        if (state.Choice is not null)
        {
            return [new("j/k", "choose"), new("Enter", "keep"), new("Esc", "restore")];
        }

        if (state.IsFiltering)
        {
            return [new("j/k", "move"), new("Enter", "jump to it"), new("/", "new filter"), new("Esc", "clear")];
        }

        if (state.Column == SettingsColumn.Categories && !state.ListsAllCategories)
        {
            return [new("j/k", "move"), new("l/Enter", "open"), new("/", "filter"), Close];
        }

        return state.Selected switch
        {
            { Kind: SettingRowKind.Item } =>
            [
                new("Enter", "edit"),
                new("d", "remove"),
                new("J/K", "reorder"),
                new("a", "add"),
                Back,
                Close,
            ],
            { Definition.Control: SettingControl.PathList } => [new("Enter/a", "add"), Move, Back, Filter, Close],
            { Definition.Control: SettingControl.Toggle } => [new("Enter/x", "flip"), Move, Back, Filter, Close],
            { Definition.Control: SettingControl.Number } =>
                [new("Enter", "type"), new("+/-", "step"), Move, Back, Close],
            { Kind: SettingRowKind.Binding } => [new("Enter", "show config entry"), Move, Back, Filter, Close],
            { Definition.Control: SettingControl.KeyReference } => [Move, Back, Filter, Close],
            { Definition.Control: SettingControl.Action } => [new("Enter", "open"), Move, Back, Filter, Close],
            _ => [Move, new("l/Enter", "edit"), Back, Filter, Close],
        };
    }

    private static int EntriesTop(SettingsState state, IReadOnlyList<SettingsBodyEntry> entries, SettingsLayout layout)
    {
        var selected = 0;
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].RowIndex == state.Cursor)
            {
                selected = index;
            }
        }

        return Top(entries.Count, selected, layout.BodyRows);
    }

    // The first shown line that keeps the selection in view, near the middle of a long list.
    private static int Top(int count, int selected, int rows) =>
        Math.Clamp(selected - (rows / 2), 0, Math.Max(0, count - rows));
}
