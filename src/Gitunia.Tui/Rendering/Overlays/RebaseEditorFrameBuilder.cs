using System.Globalization;
using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Overlays;
using Gitunia.Core.Rebase;
using Gitunia.Git;
using Gitunia.Git.Rebase;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out the rebase todo editor (ux-workflows.md, workflow 3, "Interactive rebase"): the commits in replay order with their
/// action, two rows of clickable chips (the mouse path of every key) and a footer; while a reword's message is edited
/// the same box shows the multi-line message editor instead.
/// </summary>
public static class RebaseEditorFrameBuilder
{
    private const int MaximumWidth = 110;
    private const int MaximumRows = 20;
    private const int MaximumMessageRows = 12;
    private const int ScreenMargin = 2;
    private const int FixedChromeRows = 4;
    private const int MessageChromeRows = 3;
    private const int ActionColumnWidth = 6;
    private const int ListStartLine = 2;
    private const string Note = "oldest first: the top commit is replayed first; squash and fixup meld upwards";
    private const string DiscardHint = "Esc again discards the changes to the todo";
    private const string BlankMessageHint = "A reword needs a message: type one, or Esc to cancel";

    private static readonly HintItem[] MessageHints =
    [
        new("Ctrl-g", "keeps the message"),
        new("Enter", "new line"),
        new("Esc", "cancel"),
    ];

    private static readonly HintChip[] ChipLabels =
    [
        new("pick", DefaultMouseMap.RebasePickButton),
        new("reword", DefaultMouseMap.RebaseRewordButton),
        new("squash", DefaultMouseMap.RebaseSquashButton),
        new("fixup", DefaultMouseMap.RebaseFixupButton),
        new("drop", DefaultMouseMap.RebaseDropButton),
        new("move up", DefaultMouseMap.RebaseMoveUpButton),
        new("move down", DefaultMouseMap.RebaseMoveDownButton),
        new("start", DefaultMouseMap.RebaseStartButton),
        new("cancel", DefaultMouseMap.RebaseCancelButton),
    ];

    /// <summary>The frame for a screen, or null when the screen is too small to show it.</summary>
    public static OverlayFrame? Build(
        RebaseEditorState state,
        int screenWidth,
        int screenHeight,
        GraphGlyphSet glyphs,
        ChipKeys? keys = null)
    {
        var width = Math.Min(MaximumWidth, screenWidth - ScreenMargin);
        if (width < OverlayBox.MinimumWidth)
        {
            return null;
        }

        if (state.Edit is not null)
        {
            return BuildMessage(state, screenWidth, screenHeight, width, glyphs);
        }

        return BuildList(state, screenWidth, screenHeight, width, glyphs, keys);
    }

    /// <summary>The entry index of the row on <paramref name="frameLine"/>, or null for none.</summary>
    public static int? EntryIndexAt(
        RebaseEditorState state,
        int frameLine,
        int screenWidth,
        int screenHeight,
        ChipKeys? keys = null)
    {
        var window = Window(state, screenWidth, screenHeight, keys);
        var offset = frameLine - ListStartLine;
        if (state.Edit is null && offset >= 0 && offset < window.RowCount && window.Top + offset < state.Rows.Count)
        {
            return window.Top + offset;
        }

        return null;
    }

    /// <summary>The mouse view of the chip drawn at a frame line and column; null when none.</summary>
    public static string? ChipAt(
        RebaseEditorState state,
        int frameLine,
        int frameColumn,
        int screenWidth,
        int screenHeight,
        ChipKeys? keys = null)
    {
        var window = Window(state, screenWidth, screenHeight, keys);
        var chipRows = HintChipRows.Flow(Chips(keys), FrameWidth(screenWidth));
        var chipRow = frameLine - ListStartLine - window.RowCount;
        if (state.Edit is not null || chipRow < 0 || chipRow >= chipRows.Count)
        {
            return null;
        }

        return HintChipRows.ViewAt(chipRows[chipRow], frameColumn);
    }

    /// <summary>Rows the list shows at a screen height (the page size of the half-page motions).</summary>
    public static int VisibleRows(RebaseEditorState state, int screenWidth, int screenHeight, ChipKeys? keys = null) =>
        Window(state, screenWidth, screenHeight, keys).RowCount;

    private static HintChip[] Chips(ChipKeys? keys) =>
        HintChipRows.WithKeys(ChipLabels, keys, DefaultKeymap.RebaseEditorView);

    private static OverlayFrame? BuildList(
        RebaseEditorState state,
        int screenWidth,
        int screenHeight,
        int width,
        GraphGlyphSet glyphs,
        ChipKeys? keys)
    {
        var chipRows = HintChipRows.Flow(Chips(keys), width);
        if (screenHeight - ScreenMargin - FixedChromeRows - chipRows.Count < 1)
        {
            return null;
        }

        var window = Window(state, screenWidth, screenHeight, keys);
        var lines = new List<StyledLine>
        {
            OverlayBox.Title($"Interactive rebase: {state.BranchName} onto {state.TargetLabel}", width, glyphs),
            OverlayBox.Text(Note, width, glyphs, SegmentRole.Faint),
        };
        for (var offset = 0; offset < window.RowCount && window.Top + offset < state.Rows.Count; offset++)
        {
            lines.Add(RowLine(state.Rows[window.Top + offset], width, glyphs, window.Top + offset == window.Selected));
        }

        while (lines.Count < ListStartLine + window.RowCount)
        {
            lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
        }

        foreach (var chips in chipRows)
        {
            lines.Add(HintChipRows.Line(chips, width, glyphs));
        }

        lines.Add(FooterLine(state, width, glyphs));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines);
    }

    private static OverlayFrame? BuildMessage(
        RebaseEditorState state,
        int screenWidth,
        int screenHeight,
        int width,
        GraphGlyphSet glyphs)
    {
        var rows = Math.Min(MaximumMessageRows, screenHeight - ScreenMargin - MessageChromeRows);
        if (rows < 1 || state.Edit is not { } edit)
        {
            return null;
        }

        var hash = ShortHash.Of(edit.Hash);
        var lines = new List<StyledLine> { OverlayBox.Title($"Reword {hash}", width, glyphs) };
        var textWidth = OverlayBox.InnerWidth(width) - 1;
        foreach (var line in MessageEditorRenderer.Render(edit.Editor, textWidth, rows, showCursor: true))
        {
            var padded = new StyledLine([new StyledSegment(" ", SegmentRole.Plain), .. line.Segments]);
            lines.Add(OverlayBox.Wrap(padded, glyphs));
        }

        if (edit.Editor.IsBlank)
        {
            lines.Add(OverlayBox.Text(BlankMessageHint, width, glyphs, SegmentRole.Error));
        }
        else
        {
            lines.Add(OverlayBox.Hints(MessageHints, width, glyphs, gap: PromptPopup.HintGap));
        }

        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines);
    }

    private static StyledLine RowLine(RebaseEditorRow row, int width, GraphGlyphSet glyphs, bool isSelected)
    {
        var action = ActionWord(row.Action).PadRight(ActionColumnWidth);
        if (row.IsStructure)
        {
            return OverlayBox.TwoColumns(
                $"{action} {row.Hash}",
                string.Empty,
                width,
                glyphs,
                SegmentRole.Faint,
                SegmentRole.Faint,
                isSelected);
        }

        var hash = ShortHash.Of(row.Hash);
        var role = row.Action == RebaseTodoAction.Drop ? SegmentRole.Faint : SegmentRole.Plain;
        var text = $"{action} {hash} {row.ShownSubject}";
        return OverlayBox.TwoColumns(text, string.Empty, width, glyphs, role, role, isSelected);
    }

    private static StyledLine FooterLine(RebaseEditorState state, int width, GraphGlyphSet glyphs)
    {
        if (state.IsDiscardPending)
        {
            return OverlayBox.Text(DiscardHint, width, glyphs, SegmentRole.Error);
        }

        if (state.Problem is { } problem)
        {
            return OverlayBox.Text(problem, width, glyphs, SegmentRole.Error);
        }

        var position = state.Rows.Count == 0 ? 0 : Math.Clamp(state.SelectedIndex, 0, state.Rows.Count - 1) + 1;
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"{position}/{state.Rows.Count}  {state.KeptCount} kept  {state.DroppedCount} dropped  {Modified(state)}");
        return OverlayBox.Text(text.TrimEnd(), width, glyphs, SegmentRole.Faint);
    }

    private static string Modified(RebaseEditorState state) => state.IsModified ? "edited" : "unchanged";

    private static string ActionWord(RebaseTodoAction action) => action switch
    {
        RebaseTodoAction.Pick => "pick",
        RebaseTodoAction.Reword => "reword",
        RebaseTodoAction.Squash => "squash",
        RebaseTodoAction.Fixup => "fixup",
        RebaseTodoAction.Label => "label",
        RebaseTodoAction.Reset => "reset",
        RebaseTodoAction.Merge => "merge",
        _ => "drop",
    };

    private static int FrameWidth(int screenWidth) => Math.Min(MaximumWidth, screenWidth - ScreenMargin);

    private static PaletteWindow Window(RebaseEditorState state, int screenWidth, int screenHeight, ChipKeys? keys)
    {
        var chromeRows = FixedChromeRows + HintChipRows.Flow(Chips(keys), FrameWidth(screenWidth)).Count;
        var maximumRows = Math.Min(MaximumRows, screenHeight - ScreenMargin - chromeRows);
        var count = state.Rows.Count;
        var rowCount = Math.Max(1, Math.Min(maximumRows, count));
        var selected = count == 0 ? 0 : Math.Clamp(state.SelectedIndex, 0, count - 1);
        return new PaletteWindow(
            rowCount,
            selected,
            Math.Clamp(selected - (rowCount / 2), 0, Math.Max(0, count - rowCount)));
    }
}
