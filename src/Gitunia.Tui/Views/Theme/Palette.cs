using Gitunia.Tui.Rendering.Styling;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;
using StyledSegment = Gitunia.Tui.Rendering.Styling.StyledSegment;

namespace Gitunia.Tui.Views.Theme;

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

    /// <summary>
    /// The scheme of a view that paints rows: the toolkit clears everything the rows do not paint (the lines below the last row, which it also
    /// repaints with the normal colours when the cursor reaches the last row) with the normal colour of the view's scheme.
    /// </summary>
    public static Scheme BaseScheme() => BaseScheme(table);

    public static Scheme BaseScheme(PaletteTable colors) => new(new Attribute(colors.BaseForeground, colors.BaseBackground));

    /// <summary>Distinct lane colours of the configured scheme: the colour slots the graph layout may use.</summary>
    public static int LaneColorCount => table.Lanes.Length;

    public static Attribute For(StyledSegment segment, bool isSelected, bool isFocused) => For(segment, isSelected, isFocused, table);

    public static Attribute For(StyledSegment segment, SegmentHighlight highlight) => For(segment, highlight, table);

    public static Attribute For(StyledSegment segment, SegmentHighlight highlight, PaletteTable colors) =>
        For(segment, highlight.IsSelected, highlight.IsFocused, colors, highlight.IsRange);

    /// <param name="segment">The text to draw.</param>
    /// <param name="isSelected">The segment is on the selected row (or in a selected range).</param>
    /// <param name="isFocused">The pane holding the row has focus: the selection is the full tint, otherwise the quieter one.</param>
    /// <param name="colors">The colours of the scheme.</param>
    /// <param name="isRange">The row is in a Visual range (#814): the stronger range tint and style instead of the selection ones.</param>
    public static Attribute For(
        StyledSegment segment,
        bool isSelected,
        bool isFocused,
        PaletteTable colors,
        bool isRange = false)
    {
        var role = Role(segment, colors);
        var foreground = role.Foreground == Color.None ? colors.BaseForeground : role.Foreground;
        var background = role.Background;
        var style = role.Style;
        if (!isSelected)
        {
            var plain = BackgroundOf(segment, background, colors);
            return new Attribute(foreground, plain == Color.None ? colors.BaseBackground : plain, style);
        }

        if (isRange)
        {
            return RangeAttribute(segment, role, foreground, background, style, colors);
        }

        if (!role.KeepsBackground)
        {
            background = isFocused ? colors.SelectionFocused : colors.SelectionDim;
            if (segment.Role == SegmentRole.WhitespaceMark)
            {
                foreground = colors.SubtleOnSelection;
            }
            else if (foreground == colors.Muted)
            {
                foreground = colors.MutedOnSelection;
            }
        }

        return new Attribute(foreground, background, style | (isFocused ? colors.SelectionFocusedStyle : colors.SelectionDimStyle));
    }

    // A row of a Visual range: the range tint (or the style alone where the scheme has none) and the range style, whatever the focus.
    private static Attribute RangeAttribute(
        StyledSegment segment,
        RoleAppearance role,
        Color foreground,
        Color background,
        TextStyle style,
        PaletteTable colors)
    {
        if (!role.KeepsBackground)
        {
            background = colors.RangeBackground;
            if (segment.Role == SegmentRole.WhitespaceMark)
            {
                foreground = colors.SubtleOnRange;
            }
            else if (foreground == colors.Muted)
            {
                foreground = colors.MutedOnRange;
            }
        }

        return new Attribute(foreground, background, style | colors.RangeStyle);
    }

    // A branch label of the graph: a chip in the colour of its lane (inverse video where there are no colours).
    private static RoleAppearance LaneLabel(int colorIndex, PaletteTable colors)
    {
        var index = colorIndex % colors.Lanes.Length;
        var foreground = colors.LaneLabelForegrounds?[index] ?? colors.ChipForeground;

        return new RoleAppearance(foreground, colors.Lanes[index], colors.ChipStyle, true);
    }

    private static Color BackgroundOf(StyledSegment segment, Color background, PaletteTable colors)
    {
        if (background != Color.None)
        {
            return background;
        }

        if (segment.InGroup)
        {
            return colors.GroupBackground;
        }

        return background;
    }

    // Foreground, background, style, and whether the background stays when the row is selected (chips, search matches).
    private static RoleAppearance Role(StyledSegment segment, PaletteTable colors)
    {
        var muted = colors.MutedStyle;
        return segment.Role switch
        {
            SegmentRole.Lane => new RoleAppearance(colors.Lanes[segment.ColorIndex % colors.Lanes.Length], Color.None, TextStyle.None, false),
            SegmentRole.LaneLabel => LaneLabel(segment.ColorIndex, colors),
            SegmentRole.Hash or SegmentRole.Faint => new RoleAppearance(colors.Muted, Color.None, muted, false),
            SegmentRole.WhitespaceMark => new RoleAppearance(colors.Subtle, Color.None, colors.SubtleStyle, false),
            SegmentRole.WhitespaceMarkAddedWord =>
                new RoleAppearance(
                    colors.SubtleOnSelection, colors.AddedWord, colors.SubtleStyle | colors.WordStyle, true),
            SegmentRole.WhitespaceMarkRemovedWord =>
                new RoleAppearance(
                    colors.SubtleOnSelection, colors.RemovedWord, colors.SubtleStyle | colors.WordStyle, true),
            SegmentRole.Punctuation => new RoleAppearance(colors.Muted, Color.None, muted, false),
            SegmentRole.LocalBranch => new RoleAppearance(colors.Good, Color.None, TextStyle.Bold, false),
            SegmentRole.CurrentBranch => new RoleAppearance(colors.Info, Color.None, TextStyle.Bold | colors.CurrentStyle, false),
            SegmentRole.RemoteBranch => new RoleAppearance(colors.Remote, Color.None, TextStyle.Bold, false),
            SegmentRole.Tag => new RoleAppearance(colors.Warn, Color.None, TextStyle.Bold, false),
            SegmentRole.BarButton => new RoleAppearance(colors.Accent, colors.BarBackground, TextStyle.Bold, true),
            SegmentRole.Status => new RoleAppearance(colors.Muted, colors.BarBackground, muted, true),
            SegmentRole.StatusKey => new RoleAppearance(colors.Accent, colors.BarBackground, TextStyle.Bold, true),
            SegmentRole.StatusError => new RoleAppearance(colors.Bad, colors.BarBackground, TextStyle.Bold, true),
            SegmentRole.StatusNotice => new RoleAppearance(colors.Good, colors.BarBackground, TextStyle.Bold, true),
            SegmentRole.StatusProgress => new RoleAppearance(colors.Info, colors.BarBackground, TextStyle.Bold, true),
            SegmentRole.StatusWarning => new RoleAppearance(colors.Warn, colors.BarBackground, TextStyle.Bold, true),
            SegmentRole.StatusNoticeFresh =>
                new RoleAppearance(colors.ChipForeground, colors.Good, colors.ChipStyle, true),
            SegmentRole.StatusWarningFresh =>
                new RoleAppearance(colors.ChipForeground, colors.Warn, colors.ChipStyle, true),
            SegmentRole.StatusErrorFresh =>
                new RoleAppearance(colors.ChipForeground, colors.Bad, colors.ChipStyle, true),
            SegmentRole.StatusOperation =>
                new RoleAppearance(colors.ChipForeground, colors.Warn, colors.ChipStyle, true),
            SegmentRole.StatusMode or SegmentRole.ActiveTab or SegmentRole.OverlayTitle or SegmentRole.Cursor or SegmentRole.JumpLabel => new RoleAppearance(colors.ChipForeground, colors.ChipBackground, colors.ChipStyle, true),
            SegmentRole.RepositoryTab => new RoleAppearance(Color.None, colors.TabButtonBackground, colors.SelectionDimStyle, true),
            SegmentRole.RepositoryTabKey =>
                new RoleAppearance(
                    Color.None, colors.TabButtonBackground, colors.SelectionDimStyle | TextStyle.Bold, true),
            SegmentRole.SelectedTab =>
                new RoleAppearance(
                    colors.Accent, colors.SelectionFocused, colors.SelectionDimStyle | TextStyle.Bold, true),
            SegmentRole.Error => new RoleAppearance(colors.Bad, Color.None, TextStyle.Bold, false),
            SegmentRole.SearchMatch => new RoleAppearance(colors.MatchForeground, colors.MatchBackground, colors.MatchStyle, true),
            SegmentRole.SectionTitle => new RoleAppearance(colors.Accent, Color.None, TextStyle.Bold, false),
            SegmentRole.Border => new RoleAppearance(colors.Border, Color.None, TextStyle.None, false),
            SegmentRole.FocusedBorder => new RoleAppearance(colors.Accent, Color.None, TextStyle.Bold, false),
            SegmentRole.FileName => new RoleAppearance(Color.None, Color.None, TextStyle.Bold, false),
            SegmentRole.Link => new RoleAppearance(colors.Accent, Color.None, TextStyle.Underline, false),
            SegmentRole.FileAdded => new RoleAppearance(colors.Good, Color.None, TextStyle.Bold, false),
            SegmentRole.FileDeleted => new RoleAppearance(colors.Bad, Color.None, TextStyle.Bold, false),
            SegmentRole.FileChanged => new RoleAppearance(colors.Warn, Color.None, TextStyle.Bold, false),
            SegmentRole.DiffAdded => new RoleAppearance(colors.Good, Color.None, TextStyle.None, false),
            SegmentRole.DiffRemoved => new RoleAppearance(colors.Bad, Color.None, TextStyle.None, false),
            SegmentRole.DiffAddedWord =>
                new RoleAppearance(colors.Good, colors.AddedWord, colors.WordStyle, true),
            SegmentRole.DiffRemovedWord =>
                new RoleAppearance(colors.Bad, colors.RemovedWord, colors.WordStyle, true),
            SegmentRole.DiffHunk => new RoleAppearance(colors.Info, Color.None, TextStyle.None, false),
            SegmentRole.DiffGroupBar => new RoleAppearance(colors.Accent, Color.None, TextStyle.Bold, false),
            SegmentRole.DiffFile => new RoleAppearance(colors.Accent, Color.None, TextStyle.Bold, false),
            SegmentRole.Button => new RoleAppearance(colors.Accent, Color.None, TextStyle.Bold, false),
            SegmentRole.OverlayKey => new RoleAppearance(colors.Warn, Color.None, TextStyle.Bold, false),
            _ => new RoleAppearance(Color.None, Color.None, TextStyle.None, false),
        };
    }
}
