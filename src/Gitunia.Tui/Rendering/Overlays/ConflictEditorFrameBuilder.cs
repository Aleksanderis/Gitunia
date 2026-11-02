using Gitunia.Core.Conflicts;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out the conflict editor (ux-workflows.md, workflow 4, "Conflict editor"), full screen like KDiff3: the base, ours and
/// theirs versions of the whole file side by side on top (fewer panes on a narrow terminal, <c>Tab</c> switches), the
/// whole result below it, clickable key hints at the bottom of the frame. The top border carries the file and the
/// position (<c>conflict 2 of 5</c>); the status bar below the frame keeps the key hints of the keyboard.
/// </summary>
public static partial class ConflictEditorFrameBuilder
{
    /// <summary>The narrowest terminal the editor is drawn on; the keys still work below it.</summary>
    public const int MinimumWidth = 40;

    /// <summary>The shortest frame (the terminal minus the status bar) the editor is drawn on.</summary>
    public const int MinimumHeight = 11;

    /// <summary>The terminal width from which the base, ours and theirs are drawn side by side.</summary>
    public const int ThreePaneFromWidth = 100;

    /// <summary>The terminal width from which two version panes are drawn side by side (below it, one).</summary>
    public const int TwoPaneFromWidth = 66;

    private const int ChromeRows = 5;
    private const int MinimumPaneRows = 2;
    private const int ThreeChipRowsFromHeight = 32;
    private const int TwoChipRowsFromHeight = 20;
    private const int VersionShareOf = 20;
    private const int VersionShare = 9;
    private const string DiscardHint = "Esc again discards your changes";

    private static readonly HintChip[] VersionChips =
    [
        new("next conflict", DefaultMouseMap.ConflictNextButton),
        new("previous conflict", DefaultMouseMap.ConflictPreviousButton),
        new("take", DefaultMouseMap.ConflictTakeButton),
        new("untake", DefaultMouseMap.ConflictUntakeButton),
        new("ours", DefaultMouseMap.ConflictOursButton),
        new("theirs", DefaultMouseMap.ConflictTheirsButton),
        new("base", DefaultMouseMap.ConflictBaseButton),
        new("both", DefaultMouseMap.ConflictBothButton),
        new("save", DefaultMouseMap.ConflictSaveButton),
        new("close", DefaultMouseMap.ConflictCancelButton),
        new("select lines", DefaultMouseMap.ConflictSelectButton),
        new("edit in result", DefaultMouseMap.ConflictEditButton),
        new("undo", DefaultMouseMap.ConflictUndoButton),
        new("redo", DefaultMouseMap.ConflictRedoButton),
        new("next pane", DefaultMouseMap.ConflictPaneButton),
        new("all ours", DefaultMouseMap.ConflictAllOursButton),
        new("all theirs", DefaultMouseMap.ConflictAllTheirsButton),
        new("re-create", DefaultMouseMap.ConflictRecreateButton),
        new("scroll left", DefaultMouseMap.ConflictScrollLeftButton),
        new("scroll right", DefaultMouseMap.ConflictScrollRightButton),
    ];

    private static readonly HintChip[] ResultChips =
    [
        new("next conflict", DefaultMouseMap.ConflictNextButton),
        new("previous conflict", DefaultMouseMap.ConflictPreviousButton),
        new("untake", DefaultMouseMap.ConflictUntakeButton),
        new("type", DefaultMouseMap.ConflictEditButton),
        new("save", DefaultMouseMap.ConflictSaveButton),
        new("close", DefaultMouseMap.ConflictCancelButton),
        new("undo", DefaultMouseMap.ConflictUndoButton),
        new("redo", DefaultMouseMap.ConflictRedoButton),
        new("next pane", DefaultMouseMap.ConflictPaneButton),
        new("re-create", DefaultMouseMap.ConflictRecreateButton),
    ];

    private static readonly HintChip[] VisualChips =
    [
        new("take lines", DefaultMouseMap.ConflictTakeButton),
        new("untake lines", DefaultMouseMap.ConflictUntakeButton),
        new("end selection", DefaultMouseMap.ConflictSelectButton),
    ];

    private static readonly HintChip[] EditingChips =
    [
        new("keep", DefaultMouseMap.ConflictEditDoneButton),
    ];

    /// <summary>
    /// Where the parts sit at a screen size, or null when the screen is too small to draw the editor.
    /// </summary>
    /// <param name="state">The editor.</param>
    /// <param name="screenWidth">Columns of the terminal.</param>
    /// <param name="frameHeight">Rows available to the frame: the terminal height minus the status bar.</param>
    /// <param name="keys">Where the chips take their key text from; null uses the default keymap.</param>
    public static ConflictEditorLayout? LayoutFor(
        ConflictEditorState state,
        int screenWidth,
        int frameHeight,
        ChipKeys? keys = null)
    {
        if (Measure(ChipSetOf(state), screenWidth, frameHeight, keys) is not { } measure)
        {
            return null;
        }

        var firstChipLine = ConflictEditorLayout.VersionStartLine + measure.VersionRows + 2 + measure.ResultRows + 1;

        return new ConflictEditorLayout(
            screenWidth,
            PanesFor(state, screenWidth),
            measure.VersionRows,
            measure.ResultRows,
            measure.ChipRows,
            firstChipLine);
    }

    /// <summary>
    /// The pane rows for every set of hint chips the editor shows, or null when the screen is too small to draw the
    /// editor. The reducer scrolls the panes to the cursor with the set that fits the state it computed.
    /// </summary>
    /// <param name="screenWidth">Columns of the terminal.</param>
    /// <param name="frameHeight">Rows available to the frame: the terminal height minus the status bar.</param>
    /// <param name="keys">Where the chips take their key text from; null uses the default keymap.</param>
    public static ConflictViewports? ViewportsFor(int screenWidth, int frameHeight, ChipKeys? keys = null)
    {
        if (Measure(ChipSetKind.Versions, screenWidth, frameHeight, keys) is not { } versions
            || Measure(ChipSetKind.Result, screenWidth, frameHeight, keys) is not { } result
            || Measure(ChipSetKind.Visual, screenWidth, frameHeight, keys) is not { } visual
            || Measure(ChipSetKind.Editing, screenWidth, frameHeight, keys) is not { } editing)
        {
            return null;
        }

        return new ConflictViewports(versions.Viewport, result.Viewport, visual.Viewport, editing.Viewport);
    }

    private static PaneMeasure? Measure(ChipSetKind kind, int screenWidth, int frameHeight, ChipKeys? keys)
    {
        if (screenWidth < MinimumWidth || frameHeight < MinimumHeight)
        {
            return null;
        }

        var chipRows = ChipRowsFor(kind, screenWidth, frameHeight, keys);
        var available = frameHeight - ChromeRows - ChipBlock(chipRows);
        if (available < MinimumPaneRows * 2)
        {
            chipRows = [];
            available = frameHeight - ChromeRows;
        }

        var versionRows = Math.Max(MinimumPaneRows, available * VersionShare / VersionShareOf);

        return new PaneMeasure(versionRows, available - versionRows, chipRows);
    }

    /// <summary>The frame for a screen, or null when the screen is too small to show it.</summary>
    public static OverlayFrame? Build(
        ConflictEditorState state,
        int screenWidth,
        int frameHeight,
        GraphGlyphSet glyphs,
        ChipKeys? keys = null)
    {
        if (state.Hunks.Count == 0 || LayoutFor(state, screenWidth, frameHeight, keys) is not { } layout)
        {
            return null;
        }

        var view = state.Follow(layout.VersionRows, layout.ResultRows);
        var widths = PaneWidths(layout);
        var lines = new List<StyledLine>
        {
            TopBorder(Title(view), layout.Width, glyphs),
            HeaderLine(view, layout, widths, glyphs),
        };
        for (var row = 0; row < layout.VersionRows; row++)
        {
            lines.Add(VersionLine(view, layout, widths, view.TopScroll + row, glyphs));
        }

        lines.Add(Rule(layout.Width, glyphs, DividerColumns(widths)));
        lines.Add(ResultHeader(view, layout.Width, glyphs));
        lines.AddRange(ResultLines(view, layout, glyphs));
        if (layout.ChipRows.Count > 0)
        {
            lines.Add(Rule(layout.Width, glyphs, []));
            lines.AddRange(layout.ChipRows.Select(chips => HintChipRows.Line(chips, layout.Width, glyphs)));
        }

        lines.Add(OverlayBox.Bottom(layout.Width, glyphs));

        return new OverlayFrame(0, 0, lines);
    }

    /// <summary>What a click at a frame line and column hit; null when nothing clickable.</summary>
    public static ConflictTarget? TargetAt(
        ConflictEditorState state,
        int frameLine,
        int frameColumn,
        int screenWidth,
        int frameHeight,
        ChipKeys? keys = null)
    {
        if (LayoutFor(state, screenWidth, frameHeight, keys) is not { } layout)
        {
            return null;
        }

        if (ChipAt(state, frameLine, frameColumn, screenWidth, frameHeight, keys) is { } view)
        {
            return ConflictTarget.OfChip(view);
        }

        var followed = state.Follow(layout.VersionRows, layout.ResultRows);
        var inner = frameColumn - 1;
        if (inner < 0 || inner >= OverlayBox.InnerWidth(layout.Width))
        {
            return null;
        }

        var resultHeader = ConflictEditorLayout.VersionStartLine + layout.VersionRows + 1;
        if (frameLine == ConflictEditorLayout.VersionStartLine - 1)
        {
            return PaneAt(layout, inner) is { } pane ? ConflictTarget.OfHeader(pane) : null;
        }

        if (frameLine >= ConflictEditorLayout.VersionStartLine
            && frameLine < ConflictEditorLayout.VersionStartLine + layout.VersionRows)
        {
            var row = followed.TopScroll + frameLine - ConflictEditorLayout.VersionStartLine;

            return PaneAt(layout, inner) is { } pane ? ConflictTarget.OfVersionRow(pane, row) : null;
        }

        if (frameLine == resultHeader)
        {
            return ConflictTarget.OfHeader(ConflictPane.Result);
        }

        if (frameLine >= layout.ResultStartLine && frameLine < layout.ResultStartLine + layout.ResultRows)
        {
            var line = followed.ResultScroll + frameLine - layout.ResultStartLine;
            var prefix = ConflictPaneRows.ResultPrefixWidth(state);

            // The cursor's own line is scrolled sideways while it is drawn with the cursor (the active result pane).
            var offset = 0;
            if (followed.ActivePane == ConflictPane.Result && line == followed.Result.Editor.CursorRow)
            {
                offset = MessageEditorRenderer.ScrollOffset(
                    followed.Result.Editor,
                    OverlayBox.InnerWidth(layout.Width) - prefix);
            }

            return ConflictTarget.OfResultLine(line, Math.Max(0, inner - prefix) + offset);
        }

        return null;
    }

    /// <summary>
    /// The aligned row of a version pane under a frame line while a drag goes on: a line above or below the version
    /// panes gives the first or last row shown, so the selection keeps growing; null when the screen shows no editor.
    /// </summary>
    public static int? VersionRowAtLine(
        ConflictEditorState state,
        int frameLine,
        int screenWidth,
        int frameHeight,
        ChipKeys? keys = null)
    {
        if (LayoutFor(state, screenWidth, frameHeight, keys) is not { } layout)
        {
            return null;
        }

        var followed = state.Follow(layout.VersionRows, layout.ResultRows);
        var shown = Math.Clamp(
            frameLine - ConflictEditorLayout.VersionStartLine,
            0,
            layout.VersionRows - 1);

        return Math.Clamp(followed.TopScroll + shown, 0, Math.Max(0, state.Alignment.RowCount - 1));
    }

    // The version pane under a column inside the borders; the dividers between the panes belong to none.
    private static ConflictPane? PaneAt(ConflictEditorLayout layout, int innerColumn)
    {
        var widths = PaneWidths(layout);
        var start = 0;
        for (var index = 0; index < widths.Count; index++)
        {
            if (innerColumn < start)
            {
                return null;
            }

            if (innerColumn < start + widths[index])
            {
                return layout.Panes[index];
            }

            start += widths[index] + 1;
        }

        return null;
    }

    /// <summary>The mouse view of the chip drawn at a frame line and column; null when none.</summary>
    public static string? ChipAt(
        ConflictEditorState state,
        int frameLine,
        int frameColumn,
        int screenWidth,
        int frameHeight,
        ChipKeys? keys = null)
    {
        if (LayoutFor(state, screenWidth, frameHeight, keys) is not { } layout)
        {
            return null;
        }

        var chipRow = frameLine - layout.FirstChipLine;
        if (chipRow < 0 || chipRow >= layout.ChipRows.Count)
        {
            return null;
        }

        return HintChipRows.ViewAt(layout.ChipRows[chipRow], frameColumn);
    }

    private static List<ConflictPane> PanesFor(ConflictEditorState state, int screenWidth)
    {
        var focus = state.ActivePane == ConflictPane.Result ? state.TopPane : state.ActivePane;
        if (screenWidth >= ThreePaneFromWidth)
        {
            return [ConflictPane.Base, ConflictPane.Ours, ConflictPane.Theirs];
        }

        if (screenWidth < TwoPaneFromWidth)
        {
            return [focus];
        }

        return focus == ConflictPane.Base
            ? [ConflictPane.Base, ConflictPane.Ours]
            : [ConflictPane.Ours, ConflictPane.Theirs];
    }

    // The columns of each version pane; the dividers between them take one column each.
    private static List<int> PaneWidths(ConflictEditorLayout layout)
    {
        var count = layout.Panes.Count;
        var inner = OverlayBox.InnerWidth(layout.Width);
        var each = (inner - (count - 1)) / count;
        var widths = Enumerable.Repeat(each, count).ToList();
        widths[count - 1] = inner - (count - 1) - (each * (count - 1));

        return widths;
    }

    private static List<int> DividerColumns(List<int> widths)
    {
        var columns = new List<int>();
        var position = 0;
        for (var index = 0; index < widths.Count - 1; index++)
        {
            position += widths[index];
            columns.Add(position);
            position++;
        }

        return columns;
    }

    private sealed record PaneMeasure(int VersionRows, int ResultRows, List<List<HintChip>> ChipRows)
    {
        public ConflictViewport Viewport => new(VersionRows, ResultRows);
    }
}
