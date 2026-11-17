using System.Globalization;
using Gitunia.Core.Hints;
using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Core.Mouse;
using Gitunia.Core.Overlays;
using Gitunia.Core.Reflog;
using Gitunia.Core.Text;
using Gitunia.Git.Reflog.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Lays out the reflog list: hash, selector, action and message left, the relative time right.</summary>
public static class ReflogFrameBuilder
{
    private const int MaximumWidth = 110;
    private const int MaximumRows = 20;
    private static int ChromeRows => 3 + ActionRows;
    private const int ScreenMargin = 2;

    // What a key does to the highlighted entry, one row each: the hints of the reflog that have a sentence. A click on a
    // row runs the action (the mouse view of the action says which click binding).
    private static readonly List<KeyHint> Actions = ActionRow.RowsOf(HintSet.ForReflogList());

    private static int ActionRows => Actions.Count;

    /// <summary>The reflog frame for a screen, or null when the screen is too small to show it.</summary>
    public static OverlayFrame? Build(
        ReflogState state,
        int screenWidth,
        int screenHeight,
        GraphGlyphSet glyphs,
        DateTimeOffset now,
        ChipKeys? keys = null)
    {
        var width = OverlayMetrics.WidthFor(MaximumWidth, screenWidth, ScreenMargin);
        var maximumRows = Math.Min(MaximumRows, screenHeight - ScreenMargin - ChromeRows);
        if (width < OverlayBox.MinimumWidth || maximumRows < 1)
        {
            return null;
        }

        if (state.Picker is { } picker)
        {
            return BuildPicker(state, picker, screenWidth, width, maximumRows, glyphs);
        }

        var window = Window(state.Entries.Count, state.SelectedIndex, maximumRows);
        var lines = new List<StyledLine> { OverlayBox.Title($"Reflog: {state.Title}", width, glyphs) };
        if (state.Entries.Count == 0)
        {
            lines.Add(OverlayBox.Text("no reflog entries", width, glyphs, SegmentRole.Faint));
        }

        var selectorWidth = SelectorWidth(state, window.Top, window.RowCount);
        for (var offset = 0; offset < window.RowCount && window.Top + offset < state.Entries.Count; offset++)
        {
            var entry = state.Entries[window.Top + offset];
            var hash = ShortHash.Of(entry.Hash);
            var message = entry.Message.Length == 0 ? entry.Action : $"{entry.Action}: {entry.Message}";
            var selector = Selector(state, entry).PadRight(selectorWidth);
            lines.Add(OverlayBox.TwoColumns(
                $"{hash}  {selector}  {message}",
                ValueFormat.Relative(entry.Date, now),
                width,
                glyphs,
                SegmentRole.Plain,
                SegmentRole.Faint,
                window.Top + offset == window.Selected));
        }

        while (lines.Count < window.RowCount + 1)
        {
            lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
        }

        lines.AddRange(Actions.Select(action => ActionRow.Line(action, DefaultKeymap.ReflogView, width, glyphs, keys)));

        var targets = new List<FrameTarget>();
        lines.Add(OverlayBox.Hints(
            ActionRow.FooterOf(HintSet.ForReflog(state)),
            targets,
            lines.Count,
            width,
            glyphs,
            FooterLead(state),
            field: FooterField(state)));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines, targets);
    }

    /// <summary>The mouse view of the action row drawn on a frame line; null when the line is none.</summary>
    public static string? ActionAt(ReflogState state, int frameLine, int screenHeight)
    {
        if (state.Picker is not null)
        {
            return null;
        }

        var rows = VisibleRows(state, screenHeight);
        var index = frameLine - 1 - rows;
        return index >= 0 && index < Actions.Count ? DefaultMouseMap.ClickViewOf(Actions[index].Actions[0]) : null;
    }

    /// <summary>The choice of the picker drawn on a frame line, an index into its candidates; null for none.</summary>
    public static int? PickerIndexAt(ReflogRefPicker picker, int frameLine, int screenHeight)
    {
        var window = Window(
            picker.Candidates.Count,
            picker.Index,
            Math.Min(MaximumRows, screenHeight - ScreenMargin - ChromeRows));
        var offset = frameLine - 1;
        if (offset >= 0 && offset < window.RowCount && window.Top + offset < picker.Candidates.Count)
        {
            return window.Top + offset;
        }

        return null;
    }

    private static OverlayFrame BuildPicker(
        ReflogState state,
        ReflogRefPicker picker,
        int screenWidth,
        int width,
        int maximumRows,
        GraphGlyphSet glyphs)
    {
        var candidates = picker.Candidates;
        var window = Window(candidates.Count, picker.Index, maximumRows);
        var title = FilterListFrame.Title("Reflog: choose a ref", null, picker.List.Position, width, glyphs);
        var lines = new List<StyledLine> { title };
        if (picker.List.EmptyText is { } empty)
        {
            lines.Add(FilterListFrame.EmptyRow(empty, width, glyphs));
        }

        for (var offset = 0; offset < window.RowCount && window.Top + offset < candidates.Count; offset++)
        {
            lines.Add(OverlayBox.TwoColumns(
                candidates[window.Top + offset],
                string.Empty,
                width,
                glyphs,
                SegmentRole.Plain,
                SegmentRole.Faint,
                window.Top + offset == window.Selected));
        }

        while (lines.Count < window.RowCount + 1 + ActionRows)
        {
            lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
        }

        var targets = new List<FrameTarget>();
        lines.Add(OverlayBox.Hints(
            HintSet.ForReflog(state),
            targets,
            lines.Count,
            width,
            glyphs,
            field: CaretField.Of($"{glyphs.Picker.Prompt} ", picker.Field)));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines, targets);
    }

    private static string Selector(ReflogState state, ReflogEntry entry) =>
        $"{state.Title}@{{{entry.Index.ToString(CultureInfo.InvariantCulture)}}}";

    // The selector column is as wide as the widest selector shown, so the message column does not move at HEAD@{10}.
    private static int SelectorWidth(ReflogState state, int top, int rowCount)
    {
        var widest = 0;
        for (var index = top; index < top + rowCount && index < state.Entries.Count; index++)
        {
            widest = Math.Max(widest, TextWidth.Of(Selector(state, state.Entries[index])));
        }

        return widest;
    }

    private static string FooterLead(ReflogState state)
    {
        if (!state.HasSearch)
        {
            var position = state.Entries.Count == 0 ? 0 : state.MoveTo(state.SelectedIndex).SelectedIndex + 1;
            return string.Create(CultureInfo.InvariantCulture, $"{position}/{state.Entries.Count}");
        }

        return $"/{state.SearchText}  {SearchCount(state)}";
    }

    private static string SearchCount(ReflogState state)
    {
        var matches = state.MatchIndexes;
        if (matches.Count == 0)
        {
            return UiTexts.NoMatch("entry");
        }

        var current = matches.ToList().IndexOf(state.SelectedIndex) + 1;

        return string.Create(CultureInfo.InvariantCulture, $"{current}/{matches.Count} matches");
    }

    // The search being typed: drawn with a cursor cell instead of the plain lead.
    private static CaretField? FooterField(ReflogState state)
    {
        if (!state.HasSearch || !state.IsEditingSearch)
        {
            return null;
        }

        return CaretField.Of("/", state.Search, $"  {SearchCount(state)}");
    }

    /// <summary>The entry index of the row on <paramref name="frameLine"/>, or null for none.</summary>
    public static int? EntryIndexAt(ReflogState state, int frameLine, int screenHeight)
    {
        var window = Window(
            state.Entries.Count,
            state.SelectedIndex,
            Math.Min(MaximumRows, screenHeight - ScreenMargin - ChromeRows));
        var offset = frameLine - 1;
        if (offset >= 0 && offset < window.RowCount && window.Top + offset < state.Entries.Count)
        {
            return window.Top + offset;
        }

        return null;
    }

    /// <summary>Rows the list shows at a screen height (the page size of the half-page motions).</summary>
    public static int VisibleRows(ReflogState state, int screenHeight) =>
        Window(
            state.Entries.Count,
            state.SelectedIndex,
            Math.Min(MaximumRows, screenHeight - ScreenMargin - ChromeRows)).RowCount;

    private static ListWindow Window(int count, int selectedIndex, int maximumRows) =>
        ListViewport.Of(selectedIndex, count, maximumRows).Follow(ScrollPolicy.Centre).Window;
}
