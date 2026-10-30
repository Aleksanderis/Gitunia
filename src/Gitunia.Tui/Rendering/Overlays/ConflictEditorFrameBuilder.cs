using System.Globalization;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
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
public static class ConflictEditorFrameBuilder
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
        if (screenWidth < MinimumWidth || frameHeight < MinimumHeight)
        {
            return null;
        }

        var chipRows = ChipRowsFor(state, screenWidth, frameHeight, keys);
        var available = frameHeight - ChromeRows - ChipBlock(chipRows);
        if (available < MinimumPaneRows * 2)
        {
            chipRows = [];
            available = frameHeight - ChromeRows;
        }

        var versionRows = Math.Max(MinimumPaneRows, available * VersionShare / VersionShareOf);
        var resultRows = available - versionRows;
        var firstChipLine = ConflictEditorLayout.VersionStartLine + versionRows + 2 + resultRows + 1;

        return new ConflictEditorLayout(
            screenWidth,
            PanesFor(state, screenWidth),
            versionRows,
            resultRows,
            chipRows,
            firstChipLine);
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
            var offset = followed.ActivePane == ConflictPane.Result && line == followed.Result.Editor.CursorRow
                ? MessageEditorRenderer.ScrollOffset(
                    followed.Result.Editor,
                    OverlayBox.InnerWidth(layout.Width) - prefix)
                : 0;

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

    private static int ChipBlock(List<List<HintChip>> chipRows) => chipRows.Count == 0 ? 0 : chipRows.Count + 1;

    private static List<List<HintChip>> ChipRowsFor(
        ConflictEditorState state,
        int screenWidth,
        int frameHeight,
        ChipKeys? keys)
    {
        var maximum = 1;
        if (frameHeight >= ThreeChipRowsFromHeight)
        {
            maximum = 3;
        }
        else if (frameHeight >= TwoChipRowsFromHeight)
        {
            maximum = 2;
        }

        return [.. HintChipRows.Flow(ChipSetFor(state, keys), screenWidth).Take(maximum)];
    }

    private static HintChip[] ChipSetFor(ConflictEditorState state, ChipKeys? keys)
    {
        if (state.IsEditing)
        {
            return HintChipRows.WithKeys(EditingChips, keys, DefaultKeymap.ConflictResultView, KeyMode.Insert);
        }

        if (state.IsVisual)
        {
            return HintChipRows.WithKeys(VisualChips, keys, DefaultKeymap.ConflictSideView, KeyMode.Visual);
        }

        if (state.IsVersionPane)
        {
            return HintChipRows.WithKeys(VersionChips, keys, DefaultKeymap.ConflictSideView);
        }

        return HintChipRows.WithKeys(ResultChips, keys, DefaultKeymap.ConflictResultView);
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

    private static string Title(ConflictEditorState state)
    {
        var position = string.Create(
            CultureInfo.InvariantCulture,
            $"conflict {state.Index + 1} of {state.Hunks.Count}");
        if (state.IsDiscardPending)
        {
            return $"Conflict: {state.Path}  {position}  {DiscardHint}";
        }

        // The counts come before the state so that a narrow terminal cuts the state text first.
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Conflict: {state.Path}  {position}  {state.ResolvedCount} resolved  {state.UnresolvedCount} unresolved  {ConflictState(state)}");
    }

    private static StyledLine HeaderLine(
        ConflictEditorState state,
        ConflictEditorLayout layout,
        List<int> widths,
        GraphGlyphSet glyphs)
    {
        var divider = Vertical(glyphs);
        var inner = new LineBuilder(OverlayBox.InnerWidth(layout.Width), glyphs.Ellipsis);
        for (var index = 0; index < layout.Panes.Count; index++)
        {
            if (index > 0)
            {
                inner.Append(divider, SegmentRole.FocusedBorder);
            }

            inner.Append(PaneHeader(state, layout.Panes[index], widths[index], glyphs));
        }

        return OverlayBox.Wrap(inner.Build(), glyphs);
    }

    private static StyledLine PaneHeader(ConflictEditorState state, ConflictPane pane, int width, GraphGlyphSet glyphs)
    {
        var hunk = state.Selected!;
        var baseLabel = hunk.Base is null ? "no common ancestor" : "common ancestor";
        var (name, label, role) = pane switch
        {
            ConflictPane.Base => ("base", baseLabel, SegmentRole.SectionTitle),
            ConflictPane.Ours => ("ours", hunk.OursLabel, SegmentRole.LocalBranch),
            _ => ("theirs", hunk.TheirsLabel, SegmentRole.RemoteBranch),
        };
        var builder = new LineBuilder(width, glyphs.Ellipsis).Append(" ", SegmentRole.Plain);
        builder.Append(name, state.ActivePane == pane ? SegmentRole.ActiveTab : role);
        if (label.Length > 0)
        {
            builder.Append($"  {label}", role);
        }

        if (state.ActivePane == pane && state.IsVisual)
        {
            var count = state.SelectionEnd - state.SelectionStart + 1;
            builder.Append(string.Create(CultureInfo.InvariantCulture, $"  VISUAL {count}"), SegmentRole.StatusMode);
        }

        return builder.Build();
    }

    private static StyledLine VersionLine(
        ConflictEditorState state,
        ConflictEditorLayout layout,
        List<int> widths,
        int row,
        GraphGlyphSet glyphs)
    {
        var divider = Vertical(glyphs);
        var inner = new LineBuilder(OverlayBox.InnerWidth(layout.Width), glyphs.Ellipsis);
        for (var index = 0; index < layout.Panes.Count; index++)
        {
            if (index > 0)
            {
                inner.Append(divider, SegmentRole.FocusedBorder);
            }

            inner.Append(ConflictPaneRows.Version(state, layout.Panes[index], row, widths[index], glyphs));
        }

        return OverlayBox.Wrap(inner.Build(), glyphs);
    }

    private static StyledLine ResultHeader(ConflictEditorState state, int width, GraphGlyphSet glyphs)
    {
        var isActive = state.ActivePane == ConflictPane.Result;
        var builder = new LineBuilder(OverlayBox.InnerWidth(width), glyphs.Ellipsis).Append(" ", SegmentRole.Plain);
        builder.Append("result", isActive ? SegmentRole.ActiveTab : SegmentRole.SectionTitle);
        var line = state.ActivePane == ConflictPane.Result
            ? state.Result.Editor.CursorRow + 1
            : state.ResultLineOfRow(state.Row) + 1;
        builder.Append(
            string.Create(CultureInfo.InvariantCulture, $"  line {line} of {state.ResultLineCount}"),
            SegmentRole.Faint);
        if (state.IsEditing)
        {
            builder.Append("  INSERT  typing in the result, Esc keeps the text", SegmentRole.StatusOperation);
        }
        else if (isActive)
        {
            builder.Append("  NORMAL  i types", SegmentRole.StatusMode);
        }

        return OverlayBox.Wrap(builder.Build(), glyphs);
    }

    private static string ConflictState(ConflictEditorState state)
    {
        if (state.Result.IsUnresolved(state.Index))
        {
            return state.SelectedPick == ConflictPick.None ? "unresolved, still holds its markers" : "markers left";
        }

        return state.SelectedPick switch
        {
            ConflictPick.Ours => "taken from ours",
            ConflictPick.Theirs => "taken from theirs",
            ConflictPick.Base => "taken from the base",
            ConflictPick.Both => "both sides, ours first",
            ConflictPick.Lines => "lines you picked",
            _ => "edited by you",
        };
    }

    private static IEnumerable<StyledLine> ResultLines(
        ConflictEditorState state,
        ConflictEditorLayout layout,
        GraphGlyphSet glyphs)
    {
        var textWidth = OverlayBox.InnerWidth(layout.Width) - ConflictPaneRows.ResultPrefixWidth(state);
        var rendered = MessageEditorRenderer.Render(
            state.Result.Editor,
            textWidth,
            layout.ResultRows,
            showCursor: state.ActivePane == ConflictPane.Result,
            scrollTop: state.ResultScroll,
            marksSubject: false);
        for (var row = 0; row < layout.ResultRows; row++)
        {
            var line = state.ResultScroll + row;
            yield return OverlayBox.Wrap(ConflictPaneRows.Result(state, line, rendered[row], glyphs), glyphs);
        }
    }

    private static string Vertical(GraphGlyphSet glyphs) =>
        glyphs.For(LineDirections.Up | LineDirections.Down).ToString();

    // A rule across the frame, joined to both side borders; `closesUp` columns meet the pane dividers above it.
    private static StyledLine Rule(int width, GraphGlyphSet glyphs, List<int> closesUp)
    {
        var inner = OverlayBox.InnerWidth(width);
        var chars = new string(glyphs.Horizontal, inner).ToCharArray();
        foreach (var column in closesUp)
        {
            chars[column] = Joint(glyphs, LineDirections.Left | LineDirections.Right | LineDirections.Up);
        }

        var opening = Joint(glyphs, LineDirections.Up | LineDirections.Down | LineDirections.Right);
        var closing = Joint(glyphs, LineDirections.Up | LineDirections.Down | LineDirections.Left);

        return new StyledLine(
        [
            new StyledSegment(opening.ToString(), SegmentRole.FocusedBorder),
            new StyledSegment(new string(chars), SegmentRole.FocusedBorder),
            new StyledSegment(closing.ToString(), SegmentRole.FocusedBorder),
        ]);
    }

    // The top border with the title set into it: `╭─ Conflict: file  conflict 1 of 2 ────╮`.
    private static StyledLine TopBorder(string title, int width, GraphGlyphSet glyphs)
    {
        var inner = OverlayBox.InnerWidth(width);
        var horizontal = glyphs.Horizontal.ToString();
        var label = TextWidth.Fit($" {title} ", Math.Max(0, inner - 1), glyphs.Ellipsis);
        var fill = Math.Max(0, inner - 1 - TextWidth.Of(label));
        var opening = Joint(glyphs, LineDirections.Down | LineDirections.Right);
        var closing = Joint(glyphs, LineDirections.Down | LineDirections.Left);

        return new StyledLine(
        [
            new StyledSegment(opening.ToString(), SegmentRole.FocusedBorder),
            new StyledSegment(horizontal, SegmentRole.FocusedBorder),
            new StyledSegment(label, SegmentRole.SectionTitle),
            new StyledSegment(string.Concat(Enumerable.Repeat(horizontal, fill)), SegmentRole.FocusedBorder),
            new StyledSegment(closing.ToString(), SegmentRole.FocusedBorder),
        ]);
    }

    // Corners and junctions are a plus sign in the ASCII set, as in the other overlays.
    private static char Joint(GraphGlyphSet glyphs, LineDirections directions) =>
        glyphs.IsAscii ? '+' : glyphs.For(directions);
}
