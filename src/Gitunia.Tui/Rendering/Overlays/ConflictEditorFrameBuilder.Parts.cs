using Gitunia.Core.Conflicts;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

// Chip rows and frame borders of the conflict editor.
public static partial class ConflictEditorFrameBuilder
{
    private static int ChipBlock(List<List<HintChip>> chipRows) => chipRows.Count == 0 ? 0 : chipRows.Count + 1;

    private static List<List<HintChip>> ChipRowsFor(
        ChipSetKind kind,
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

        return [.. HintChipRows.Flow(ChipSetFor(kind, keys), screenWidth).Take(maximum)];
    }

    private static HintChip[] ChipSetFor(ChipSetKind kind, ChipKeys? keys) => kind switch
    {
        ChipSetKind.Editing => HintChipRows.WithKeys(
            EditingChips,
            keys,
            DefaultKeymap.ConflictResultView,
            KeyMode.Insert),
        ChipSetKind.Visual => HintChipRows.WithKeys(
            VisualChips,
            keys,
            DefaultKeymap.ConflictSideView,
            KeyMode.Visual),
        ChipSetKind.Versions => HintChipRows.WithKeys(VersionChips, keys, DefaultKeymap.ConflictSideView),
        _ => HintChipRows.WithKeys(ResultChips, keys, DefaultKeymap.ConflictResultView),
    };


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

    private static ChipSetKind ChipSetOf(ConflictEditorState state)
    {
        if (state.IsEditing)
        {
            return ChipSetKind.Editing;
        }

        if (state.IsVisual)
        {
            return ChipSetKind.Visual;
        }

        return state.IsVersionPane ? ChipSetKind.Versions : ChipSetKind.Result;
    }

    private enum ChipSetKind
    {
        Versions,
        Result,
        Visual,
        Editing,
    }
}
