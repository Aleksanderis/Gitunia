using System.Globalization;
using Gitunia.Core.Mouse;
using Gitunia.Core.Reflog;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Lays out the reflog list: hash, selector, action and message left, the relative time right.</summary>
public static class ReflogFrameBuilder
{
    private const int MaximumWidth = 110;
    private const int MaximumRows = 20;
    private const int ChromeRows = 3 + ActionRows;
    private const int ActionRows = 4;
    private const int ScreenMargin = 2;
    private const int ShortHashLength = 7;

    // The mouse paths of an entry: a click on a row runs the action on the highlighted entry.
    private static readonly ReflogEntryAction[] Actions =
    [
        new("Enter", "select the commit in the graph", DefaultMouseMap.ReflogSelectButton),
        new("<space>gx", "reset the current branch to it", DefaultMouseMap.ReflogResetButton),
        new("<space>bc", "create a branch here", DefaultMouseMap.ReflogBranchButton),
        new("<space>bs", "check it out (detached HEAD)", DefaultMouseMap.ReflogCheckoutButton),
    ];

    /// <summary>The reflog frame for a screen, or null when the screen is too small to show it.</summary>
    public static OverlayFrame? Build(ReflogState state, int screenWidth, int screenHeight, GraphGlyphSet glyphs)
    {
        var width = Math.Min(MaximumWidth, screenWidth - ScreenMargin);
        var maximumRows = Math.Min(MaximumRows, screenHeight - ScreenMargin - ChromeRows);
        if (width < OverlayBox.MinimumWidth || maximumRows < 1)
        {
            return null;
        }

        if (state.Picker is { } picker)
        {
            return BuildPicker(picker, screenWidth, width, maximumRows, glyphs);
        }

        var window = Window(state.Entries.Count, state.SelectedIndex, maximumRows);
        var lines = new List<StyledLine> { OverlayBox.Title($"Reflog: {state.Title}", width, glyphs) };
        if (state.Entries.Count == 0)
        {
            lines.Add(OverlayBox.Text("no reflog entries", width, glyphs, SegmentRole.Faint));
        }

        for (var offset = 0; offset < window.RowCount && window.Top + offset < state.Entries.Count; offset++)
        {
            var entry = state.Entries[window.Top + offset];
            var hash = entry.Hash[..Math.Min(ShortHashLength, entry.Hash.Length)];
            var message = entry.Message.Length == 0 ? entry.Action : $"{entry.Action}: {entry.Message}";
            var selector = $"{state.Title}@{{{entry.Index.ToString(CultureInfo.InvariantCulture)}}}";
            lines.Add(OverlayBox.TwoColumns(
                $"{hash}  {selector}  {message}",
                entry.RelativeDate,
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

        foreach (var action in Actions)
        {
            lines.Add(OverlayBox.TwoColumns(
                action.Key,
                action.Label,
                width,
                glyphs,
                SegmentRole.OverlayKey,
                SegmentRole.Faint));
        }

        lines.Add(OverlayBox.Hints(FooterHints(state), width, glyphs, FooterLead(state)));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines);
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
        return index >= 0 && index < Actions.Length ? Actions[index].View : null;
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
        ReflogRefPicker picker,
        int screenWidth,
        int width,
        int maximumRows,
        GraphGlyphSet glyphs)
    {
        var candidates = picker.Candidates;
        var window = Window(candidates.Count, picker.Index, maximumRows);
        var lines = new List<StyledLine> { OverlayBox.Title("Reflog: choose a ref", width, glyphs) };
        if (candidates.Count == 0)
        {
            lines.Add(OverlayBox.Text("no matching branch", width, glyphs, SegmentRole.Faint));
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

        lines.Add(OverlayBox.Hints([new HintItem("Enter", "open"), new HintItem("Esc", "back")], width, glyphs, $"> {picker.Text}_"));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines);
    }

    private static HintItem[] FooterHints(ReflogState state)
    {
        if (!state.HasSearch)
        {
            return [new("/", "search"), new("Esc", "close")];
        }

        return [new("Esc", state.IsEditingSearch ? "cancel" : "clear")];
    }

    private static string FooterLead(ReflogState state)
    {
        if (!state.HasSearch)
        {
            var position = state.Entries.Count == 0 ? 0 : state.MoveTo(state.SelectedIndex).SelectedIndex + 1;
            return string.Create(CultureInfo.InvariantCulture, $"{position}/{state.Entries.Count}");
        }

        var matches = state.MatchIndexes;
        var current = matches.ToList().IndexOf(state.SelectedIndex) + 1;
        var count = "no match";
        if (matches.Count > 0)
        {
            count = string.Create(CultureInfo.InvariantCulture, $"{current}/{matches.Count} matches");
        }

        var cursor = state.IsEditingSearch ? "_" : string.Empty;

        return $"/{state.SearchText}{cursor}  {count}";
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

    private static PaletteWindow Window(int count, int selectedIndex, int maximumRows)
    {
        var rowCount = Math.Max(1, Math.Min(maximumRows, count));
        var selected = count == 0 ? 0 : Math.Clamp(selectedIndex, 0, count - 1);
        return new PaletteWindow(
            rowCount,
            selected,
            Math.Clamp(selected - (rowCount / 2), 0, Math.Max(0, count - rowCount)));
    }
}
