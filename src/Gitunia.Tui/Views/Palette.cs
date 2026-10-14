using Gitunia.Git.Diff.Models;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Rendering.Styling;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;
using StyledSegment = Gitunia.Tui.Rendering.Styling.StyledSegment;

namespace Gitunia.Tui.Views;

/// <summary>
/// Maps the renderer-agnostic <see cref="SegmentRole"/>s to Terminal.Gui colours and text styles. The colours themselves are in a
/// <see cref="PaletteTable"/> chosen once at start-up from the terminal's capabilities (<see cref="Configure"/>); this class only
/// decides which role uses which colour. The selected row gets a background tint instead of inverse video, so the row keeps its own colours.
/// </summary>
public static class Palette
{
    private static PaletteTable table = PaletteTable.For(PaletteSettings.Default);

    /// <summary>Chooses the colour scheme; called once before the first frame (the UI is single-threaded).</summary>
    public static void Configure(PaletteSettings settings)
    {
        table = PaletteTable.For(settings);
    }

    public static Attribute For(StyledSegment segment, bool isSelected, bool isFocused) => For(segment, isSelected, isFocused, table);

    /// <param name="segment">The text to draw.</param>
    /// <param name="isSelected">The segment is on the selected row (or in a selected range).</param>
    /// <param name="isFocused">The pane holding the row has focus: the selection is the full tint, otherwise the quieter one.</param>
    /// <param name="colors">The colours of the scheme.</param>
    public static Attribute For(StyledSegment segment, bool isSelected, bool isFocused, PaletteTable colors)
    {
        var (foreground, background, style, keepsBackground) = Role(segment, colors);
        if (!isSelected)
        {
            return new Attribute(foreground, segment.OnPanel && background == Color.None ? colors.PanelBackground : background, style);
        }

        if (!keepsBackground)
        {
            background = isFocused ? colors.SelectionFocused : colors.SelectionDim;
            if (foreground == colors.Muted)
            {
                foreground = colors.MutedOnSelection;
            }
        }

        return new Attribute(foreground, background, style | (isFocused ? colors.SelectionFocusedStyle : colors.SelectionDimStyle));
    }

    // Foreground, background, style, and whether the background stays when the row is selected (chips, search matches).
    private static (Color Foreground, Color Background, TextStyle Style, bool KeepsBackground) Role(StyledSegment segment, PaletteTable colors)
    {
        var muted = colors.MutedStyle;
        return segment.Role switch
        {
            SegmentRole.Lane => (colors.Lanes[segment.ColorIndex % colors.Lanes.Length], Color.None, TextStyle.None, false),
            SegmentRole.Hash or SegmentRole.Faint => (colors.Muted, Color.None, muted, false),
            SegmentRole.Punctuation => (colors.Muted, Color.None, muted, false),
            SegmentRole.LocalBranch => (colors.Good, Color.None, TextStyle.Bold, false),
            SegmentRole.CurrentBranch => (colors.Info, Color.None, TextStyle.Bold | colors.CurrentStyle, false),
            SegmentRole.RemoteBranch => (colors.Remote, Color.None, TextStyle.Bold, false),
            SegmentRole.Tag => (colors.Warn, Color.None, TextStyle.Bold, false),
            SegmentRole.BarButton => (colors.Accent, colors.BarBackground, TextStyle.Bold, true),
            SegmentRole.Status => (colors.Muted, colors.BarBackground, muted, true),
            SegmentRole.StatusError => (colors.Bad, colors.BarBackground, TextStyle.Bold, true),
            SegmentRole.StatusMode or SegmentRole.ActiveTab or SegmentRole.OverlayTitle => (colors.ChipForeground, colors.ChipBackground, colors.ChipStyle, true),
            SegmentRole.TabButton => (colors.Muted, colors.BarBackground, colors.BarBackground == Color.None ? colors.MutedStyle | TextStyle.Underline : TextStyle.None, true),
            SegmentRole.InactiveTab => (Color.None, colors.SelectionDim, colors.SelectionDimStyle, true),
            SegmentRole.Error => (colors.Bad, Color.None, TextStyle.Bold, false),
            SegmentRole.SearchMatch => (colors.MatchForeground, colors.MatchBackground, colors.MatchStyle, true),
            SegmentRole.SectionTitle => (colors.Accent, Color.None, TextStyle.Bold, false),
            SegmentRole.Border => (colors.Border, Color.None, TextStyle.None, false),
            SegmentRole.FocusedBorder => (colors.Accent, Color.None, TextStyle.Bold, false),
            SegmentRole.FileAdded => (colors.Good, Color.None, TextStyle.Bold, false),
            SegmentRole.FileDeleted => (colors.Bad, Color.None, TextStyle.Bold, false),
            SegmentRole.FileChanged => (colors.Warn, Color.None, TextStyle.Bold, false),
            SegmentRole.DiffAdded => (colors.Good, Color.None, TextStyle.None, false),
            SegmentRole.DiffRemoved => (colors.Bad, Color.None, TextStyle.None, false),
            SegmentRole.DiffHunk => (colors.Info, Color.None, TextStyle.None, false),
            SegmentRole.DiffFile => (colors.Accent, Color.None, TextStyle.Bold, false),
            SegmentRole.Button => (colors.Accent, Color.None, TextStyle.Bold, false),
            SegmentRole.OverlayKey => (colors.Warn, Color.None, TextStyle.Bold, false),
            _ => (Color.None, Color.None, TextStyle.None, false),
        };
    }
}
