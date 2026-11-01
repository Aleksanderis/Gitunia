using System.Globalization;
using Gitunia.Core.Conflicts;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

// The header, version and result lines of the conflict editor.
public static partial class ConflictEditorFrameBuilder
{
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
        if (state.HorizontalScroll > 0)
        {
            builder.Append(
                string.Create(CultureInfo.InvariantCulture, $" col {state.HorizontalScroll + 1}"),
                SegmentRole.Faint);
        }

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
}
