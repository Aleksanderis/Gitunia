using System.Globalization;
using Gitunia.Core.Diff;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Hints;
using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Core.Mouse;
using Gitunia.Core.Text;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out the file history: the commits that touched the file on the left, that commit's diff of the file on the
/// right, the action row and the hints below. Pure: state and size in, styled lines out.
/// </summary>
public static class FileHistoryFrameBuilder
{
    private const int MaximumWidth = 200;
    private const int ScreenMargin = 2;

    // The screen is covered edge to edge, or the main view's side borders would show beside the frame.
    private const int HorizontalMargin = 0;

    // The title, the action row, the hints and the bottom rule.
    private const int ChromeRows = 4;
    private const int MinimumRows = 3;
    private const int MinimumListWidth = 24;
    private const int ListShareDivisor = 5;
    private const int ListShareParts = 2;
    private const int DividerWidth = 1;
    private const int MinimumDiffWidth = 10;

    private const string NoHistoryText = "No history: the file is not in any commit";
    private const string NoRangeHistoryText = "No commit changed these lines";
    private const string LoadingText = UiTexts.Loading;

    // The one action with a sentence of its own: select the commit in the graph.
    private static readonly KeyHint SelectHint = ActionRow.RowsOf(HintSet.ForFileHistory())[0];

    /// <summary>The frame for a screen, or null when the screen is too small to show it.</summary>
    /// <param name="state">The open history.</param>
    /// <param name="screenWidth">Screen columns.</param>
    /// <param name="screenHeight">Screen rows.</param>
    /// <param name="glyphs">The characters to draw with.</param>
    /// <param name="keys">The keymap lookup for the key of the action row; null uses the default keymap.</param>
    public static OverlayFrame? Build(
        FileHistoryState state,
        int screenWidth,
        int screenHeight,
        GraphGlyphSet glyphs,
        ChipKeys? keys = null)
    {
        var width = Math.Min(MaximumWidth, screenWidth - HorizontalMargin);
        var rows = VisibleRows(screenHeight);
        var inner = OverlayBox.InnerWidth(width);
        var listWidth = Math.Max(MinimumListWidth, inner * ListShareParts / ListShareDivisor);
        var diffWidth = inner - listWidth - DividerWidth;
        if (width < OverlayBox.MinimumWidth || rows < MinimumRows || diffWidth < MinimumDiffWidth)
        {
            return null;
        }

        var window = Window(state.Entries.Count, state.SelectedIndex, rows);
        var lines = new List<StyledLine> { OverlayBox.Title(TitleOf(state), width, glyphs) };
        for (var row = 0; row < rows; row++)
        {
            var list = ListCell(state, window, row, listWidth, glyphs);
            var diff = DiffCell(state, row, diffWidth, glyphs);
            lines.Add(Body(list, diff, inner, glyphs));
        }

        lines.Add(ActionRow.Line(SelectHint, DefaultKeymap.FileHistoryView, width, glyphs, keys));
        var targets = new List<FrameTarget>();
        lines.Add(OverlayBox.Hints(
            ActionRow.FooterOf(HintSet.ForFileHistory()),
            targets,
            lines.Count,
            width,
            glyphs,
            FooterLead(state)));
        lines.Add(OverlayBox.Bottom(width, glyphs));

        return new OverlayFrame((screenWidth - width) / 2, 1, lines, targets);
    }

    /// <summary>Rows the list and the diff show at a screen height (the page size of the half-page motions).</summary>
    /// <param name="screenHeight">Screen rows.</param>
    public static int VisibleRows(int screenHeight) => screenHeight - ScreenMargin - ChromeRows;

    /// <summary>The index of the commit drawn on <paramref name="frameLine"/>, or null for none.</summary>
    /// <param name="state">The open history.</param>
    /// <param name="frameLine">The frame line (0 is the title).</param>
    /// <param name="screenHeight">Screen rows.</param>
    public static int? EntryIndexAt(FileHistoryState state, int frameLine, int screenHeight)
    {
        var rows = VisibleRows(screenHeight);
        var window = Window(state.Entries.Count, state.SelectedIndex, rows);
        var offset = frameLine - 1;
        var isRow = offset >= 0 && offset < rows && window.Top + offset < state.Entries.Count;

        return isRow ? window.Top + offset : null;
    }

    /// <summary>The mouse view of the action row drawn on a frame line; null when the line is none.</summary>
    /// <param name="frameLine">The frame line (0 is the title).</param>
    /// <param name="screenHeight">Screen rows.</param>
    public static string? ActionAt(int frameLine, int screenHeight) =>
        frameLine == VisibleRows(screenHeight) + 1 ? DefaultMouseMap.FileHistorySelectButton : null;

    private static string TitleOf(FileHistoryState state)
    {
        var revision = state.Revision == "HEAD" ? "HEAD" : ShortHash.Of(state.Revision);

        if (state.Range is { } range)
        {
            return $"Line history: {Printable(state.Path)}:{range.Label} (from {revision})";
        }

        return $"File history: {Printable(state.Path)} (from {revision})";
    }

    private static string EmptyText(FileHistoryState state) => state.Range is null ? NoHistoryText : NoRangeHistoryText;

    private static StyledLine ListCell(
        FileHistoryState state,
        ListWindow window,
        int row,
        int width,
        GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        if (state.Entries.Count == 0)
        {
            return builder.Append(row == 0 ? $" {EmptyText(state)}" : string.Empty, SegmentRole.Faint).Build();
        }

        var index = window.Top + row;
        if (index >= state.Entries.Count)
        {
            return builder.Build();
        }

        var entry = state.Entries[index];
        var hash = ShortHash.Of(entry.Hash);
        var role = index == window.Selected ? SegmentRole.Cursor : SegmentRole.Plain;

        return builder.Append($" {hash} {entry.Date} {Printable(entry.Subject)}", role).Build(role);
    }

    private static StyledLine DiffCell(FileHistoryState state, int row, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        if (state.Entries.Count == 0)
        {
            return builder.Build();
        }

        if (state.Diff is not { } diff)
        {
            var text = state.DiffFailure.Length > 0 ? state.DiffFailure : LoadingText;
            var role = state.DiffFailure.Length > 0 ? SegmentRole.Error : SegmentRole.Loading;

            return builder.Append(row == 0 ? $" {text}" : string.Empty, role).Build();
        }

        var index = state.DiffScroll + row;
        if (index >= diff.Rows.Count)
        {
            return builder.Build();
        }

        var diffRow = diff.Rows[index];
        var (marker, rowRole) = StyleOf(diffRow.Kind);

        return builder.Append($" {marker}{Printable(diffRow.Text)}", rowRole).Build();
    }

    private static StyledLine Body(StyledLine list, StyledLine diff, int inner, GraphGlyphSet glyphs)
    {
        var rule = glyphs.For(LineDirections.Up | LineDirections.Down).ToString();
        var divider = new StyledSegment(rule, SegmentRole.Divider);
        var builder = new LineBuilder(inner, glyphs.Ellipsis).Append(list);
        builder.Append(new StyledLine([divider]));

        return OverlayBox.Wrap(builder.Append(diff).Build(), glyphs);
    }

    private static DiffMark StyleOf(DiffRowKind kind) => kind switch
    {
        DiffRowKind.Added => new DiffMark("+", SegmentRole.DiffAdded),
        DiffRowKind.Removed => new DiffMark("-", SegmentRole.DiffRemoved),
        DiffRowKind.HunkHeader => new DiffMark(string.Empty, SegmentRole.DiffHunk),
        DiffRowKind.FileHeader => new DiffMark(string.Empty, SegmentRole.DiffFile),
        DiffRowKind.Warning => new DiffMark(string.Empty, SegmentRole.Error),
        DiffRowKind.Note or DiffRowKind.NoNewline => new DiffMark(string.Empty, SegmentRole.Faint),
        _ => new DiffMark(" ", SegmentRole.Plain),
    };

    // A tab or other control character would shift the columns the frame was measured for.
    private static string Printable(string text) =>
        text.Replace("\t", "    ", StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal);

    private static string FooterLead(FileHistoryState state)
    {
        var position = state.Entries.Count == 0 ? 0 : ListViewport.ClampIndex(state.SelectedIndex, state.Entries.Count) + 1;

        return string.Create(CultureInfo.InvariantCulture, $"{position}/{state.Entries.Count}");
    }

    private static ListWindow Window(int count, int selectedIndex, int maximumRows) =>
        ListViewport.Of(selectedIndex, count, maximumRows).Follow(ScrollPolicy.Centre).Window;
}
