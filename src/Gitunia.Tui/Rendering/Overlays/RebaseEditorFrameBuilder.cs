using System.Globalization;
using Gitunia.Core.Mouse;
using Gitunia.Core.Rebase;
using Gitunia.Git.Rebase;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out the rebase todo editor (ux.md, workflow 3, "Interactive rebase"): the commits in replay order with their
/// action, two rows of clickable chips (the mouse path of every key) and a footer; while a reword's message is edited
/// the same box shows the multi-line message editor instead.
/// </summary>
public static class RebaseEditorFrameBuilder
{
    private const int MaximumWidth = 110;
    private const int MaximumRows = 20;
    private const int MaximumMessageRows = 12;
    private const int ScreenMargin = 2;
    private const int ListChromeRows = 6;
    private const int MessageChromeRows = 3;
    private const int ShortHashLength = 7;
    private const int ActionColumnWidth = 6;
    private const int ListStartLine = 2;
    private const int ChipStartColumn = 2;
    private const int ChipGap = 2;
    private const string ChipGapText = "  ";
    private const string Note = "oldest first: the top commit is replayed first; squash and fixup meld into the one above";
    private const string DiscardHint = "Esc again discards the changes to the todo";
    private const string BlankMessageHint = "A reword needs a message: type one, or Esc to cancel";

    private static readonly IReadOnlyList<IReadOnlyList<RebaseEditorChip>> ChipRows =
    [
        [
            new("[p pick]", DefaultMouseMap.RebasePickButton),
            new("[r reword]", DefaultMouseMap.RebaseRewordButton),
            new("[s squash]", DefaultMouseMap.RebaseSquashButton),
            new("[f fixup]", DefaultMouseMap.RebaseFixupButton),
            new("[d drop]", DefaultMouseMap.RebaseDropButton),
        ],
        [
            new("[K move up]", DefaultMouseMap.RebaseMoveUpButton),
            new("[J move down]", DefaultMouseMap.RebaseMoveDownButton),
            new("[Enter start]", DefaultMouseMap.RebaseStartButton),
            new("[Esc cancel]", DefaultMouseMap.RebaseCancelButton),
        ],
    ];

    /// <summary>The frame for a screen, or null when the screen is too small to show it.</summary>
    public static OverlayFrame? Build(RebaseEditorState state, int screenWidth, int screenHeight, GraphGlyphSet glyphs)
    {
        var width = Math.Min(MaximumWidth, screenWidth - ScreenMargin);
        if (width < OverlayBox.MinimumWidth)
        {
            return null;
        }

        return state.Edit is not null
            ? BuildMessage(state, screenWidth, screenHeight, width, glyphs)
            : BuildList(state, screenWidth, screenHeight, width, glyphs);
    }

    /// <summary>The entry index of the row on <paramref name="frameLine"/>, or null for none.</summary>
    public static int? EntryIndexAt(RebaseEditorState state, int frameLine, int screenHeight)
    {
        var window = Window(state, screenHeight);
        var offset = frameLine - ListStartLine;
        return state.Edit is null && offset >= 0 && offset < window.RowCount && window.Top + offset < state.Rows.Count
            ? window.Top + offset
            : null;
    }

    /// <summary>The mouse view of the chip drawn at a frame line and column; null when none.</summary>
    public static string? ChipAt(RebaseEditorState state, int frameLine, int frameColumn, int screenHeight)
    {
        var window = Window(state, screenHeight);
        var chipRow = frameLine - ListStartLine - window.RowCount;
        if (state.Edit is not null || chipRow < 0 || chipRow >= ChipRows.Count)
        {
            return null;
        }

        var start = ChipStartColumn;
        foreach (var chip in ChipRows[chipRow])
        {
            var end = start + TextWidth.Of(chip.Text);
            if (frameColumn >= start && frameColumn < end)
            {
                return chip.View;
            }

            start = end + ChipGap;
        }

        return null;
    }

    /// <summary>Rows the list shows at a screen height (the page size of the half-page motions).</summary>
    public static int VisibleRows(RebaseEditorState state, int screenHeight) => Window(state, screenHeight).RowCount;

    private static OverlayFrame? BuildList(
        RebaseEditorState state,
        int screenWidth,
        int screenHeight,
        int width,
        GraphGlyphSet glyphs)
    {
        if (screenHeight - ScreenMargin - ListChromeRows < 1)
        {
            return null;
        }

        var window = Window(state, screenHeight);
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

        foreach (var chips in ChipRows)
        {
            lines.Add(ChipLine(chips, width, glyphs));
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

        var hash = edit.Hash[..Math.Min(ShortHashLength, edit.Hash.Length)];
        var lines = new List<StyledLine> { OverlayBox.Title($"Reword {hash}", width, glyphs) };
        var textWidth = OverlayBox.InnerWidth(width) - 1;
        foreach (var line in MessageEditorRenderer.Render(edit.Editor, textWidth, rows, showCursor: true))
        {
            var padded = new StyledLine([new StyledSegment(" ", SegmentRole.Plain), .. line.Segments]);
            lines.Add(OverlayBox.Wrap(padded, glyphs));
        }

        lines.Add(edit.Editor.IsBlank
            ? OverlayBox.Text(BlankMessageHint, width, glyphs, SegmentRole.Error)
            : OverlayBox.Text("Alt-Enter or Ctrl-g keep the message   Enter new line   Esc cancel", width, glyphs, SegmentRole.Faint));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines);
    }

    private static StyledLine RowLine(RebaseEditorRow row, int width, GraphGlyphSet glyphs, bool isSelected)
    {
        var hash = row.Hash[..Math.Min(ShortHashLength, row.Hash.Length)];
        var action = ActionWord(row.Action).PadRight(ActionColumnWidth);
        var role = row.Action == RebaseTodoAction.Drop ? SegmentRole.Faint : SegmentRole.Plain;
        return OverlayBox.TwoColumns($"{action} {hash} {row.ShownSubject}", string.Empty, width, glyphs, role, role, isSelected);
    }

    private static StyledLine ChipLine(IReadOnlyList<RebaseEditorChip> chips, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(OverlayBox.InnerWidth(width), glyphs.Ellipsis).Append(" ", SegmentRole.Plain);
        for (var index = 0; index < chips.Count; index++)
        {
            builder.Append(index == 0 ? string.Empty : ChipGapText, SegmentRole.Plain);
            builder.Append(chips[index].Text, SegmentRole.Button);
        }

        return OverlayBox.Wrap(builder.Build(), glyphs);
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
        _ => "drop",
    };

    private static PaletteWindow Window(RebaseEditorState state, int screenHeight)
    {
        var maximumRows = Math.Min(MaximumRows, screenHeight - ScreenMargin - ListChromeRows);
        var count = state.Rows.Count;
        var rowCount = Math.Max(1, Math.Min(maximumRows, count));
        var selected = count == 0 ? 0 : Math.Clamp(state.SelectedIndex, 0, count - 1);
        return new PaletteWindow(
            rowCount,
            selected,
            Math.Clamp(selected - (rowCount / 2), 0, Math.Max(0, count - rowCount)));
    }
}
