using Gitunia.Platform;
using Terminal.Gui.Drawing;

namespace Gitunia.Tui.Views.Theme;

/// <summary>
/// The colours of one scheme: a few accents, muted text, the selection tints and the bars. Every colour decision of the UI is made
/// here and in <see cref="Palette"/> (which role uses which of these), so a theme later replaces only this table.
/// <see cref="Color.None"/> means the terminal's own default.
/// </summary>
public sealed class PaletteTable
{
    /// <summary>Focus, section titles, active tab, buttons.</summary>
    public required Color Accent { get; init; }

    /// <summary>Added, local branches.</summary>
    public required Color Good { get; init; }

    /// <summary>Removed lines and files, errors: red means something to look at, never a plain control.</summary>
    public required Color Bad { get; init; }

    /// <summary>Remote branches (#222).</summary>
    public required Color Remote { get; init; }

    /// <summary>Tags, modified files, overlay keys.</summary>
    public required Color Warn { get; init; }

    /// <summary>The current branch, diff hunks.</summary>
    public required Color Info { get; init; }

    /// <summary>Hashes, dates, line numbers, hints and everything secondary.</summary>
    public required Color Muted { get; init; }

    /// <summary>The colour muted text gets on a selected row, where <see cref="Muted"/> would be too dim.</summary>
    public required Color MutedOnSelection { get; init; }

    /// <summary>Lines between panes.</summary>
    public required Color Border { get; init; }

    /// <summary>The header and status bar, <see cref="Color.None"/> for none.</summary>
    public required Color BarBackground { get; init; }

    /// <summary>The background of the bottom pane's content, the bar colour, <see cref="Color.None"/> for none (then the frame and the tab buttons separate the pane).</summary>
    public required Color PanelBackground { get; init; }

    /// <summary>The selected row of the focused pane.</summary>
    public required Color SelectionFocused { get; init; }

    /// <summary>The selected row of a pane without focus: visible but quieter.</summary>
    public required Color SelectionDim { get; init; }

    /// <summary>The tint behind the change group under the cursor of the working-tree diff, quieter than the selection; <see cref="Color.None"/> where the bar beside the group is the only mark.</summary>
    public required Color GroupBackground { get; init; }

    /// <summary>The background of an inactive repository tab (a button on the bar), visibly apart from <see cref="BarBackground"/> (#240).</summary>
    public required Color TabButtonBackground { get; init; }

    /// <summary>Text on a <see cref="ChipBackground"/> (status mode, active tab, overlay title).</summary>
    public required Color ChipForeground { get; init; }

    /// <summary>Filled labels in the accent colour.</summary>
    public required Color ChipBackground { get; init; }

    /// <summary>Text of a search match.</summary>
    public required Color MatchForeground { get; init; }

    /// <summary>Background of a search match.</summary>
    public required Color MatchBackground { get; init; }

    /// <summary>The colours graph lanes cycle through.</summary>
    public required Color[] Lanes { get; init; }

    /// <summary>Added to selected text of the focused pane (bold where the tint alone is too faint, reverse without colour).</summary>
    public TextStyle SelectionFocusedStyle { get; init; } = TextStyle.None;

    /// <summary>Added to selected text of a pane without focus.</summary>
    public TextStyle SelectionDimStyle { get; init; } = TextStyle.None;

    /// <summary>Added to muted text (faint where there is no colour to mute it with).</summary>
    public TextStyle MutedStyle { get; init; } = TextStyle.None;

    /// <summary>Added to search matches (bold and underlined where there is no colour to mark them with).</summary>
    public TextStyle MatchStyle { get; init; } = TextStyle.None;

    /// <summary>Added to the current branch (underlined where there is no colour to tell it from other branches).</summary>
    public TextStyle CurrentStyle { get; init; } = TextStyle.None;

    /// <summary>Added to chips (bold, reverse without colour).</summary>
    public TextStyle ChipStyle { get; init; } = TextStyle.Bold;

    /// <summary>The table for a terminal: a Catppuccin 24-bit set (Mocha dark, Latte light), the 16 named colours, or styles only.</summary>
    public static PaletteTable For(PaletteSettings settings) => settings.Depth switch
    {
        ColorDepth.None => Monochrome(),
        ColorDepth.Ansi16 => settings.IsLightBackground ? Ansi16Light() : Ansi16Dark(),
        _ => settings.IsLightBackground ? TrueColorLight() : TrueColorDark(),
    };

    // Catppuccin Mocha (dark) and Latte (light), https://catppuccin.com/palette (MIT licensed colour values, see docs/decisions.md).
    private static PaletteTable TrueColorDark() => new()
    {
        Accent = Rgb(0x89b4fa),
        Good = Rgb(0xa6e3a1),
        Bad = Rgb(0xf38ba8),
        Remote = Rgb(0xcba6f7),
        Warn = Rgb(0xf9e2af),
        Info = Rgb(0x89dceb),
        Muted = Rgb(0x9399b2),
        MutedOnSelection = Rgb(0xa6adc8),
        Border = Rgb(0x6c7086),
        BarBackground = Rgb(0x181825),
        PanelBackground = Rgb(0x181825),
        SelectionFocused = Rgb(0x313244),
        SelectionDim = Rgb(0x262637),
        GroupBackground = Rgb(0x27283a),
        TabButtonBackground = Rgb(0x313244),
        ChipForeground = Rgb(0x11111b),
        ChipBackground = Rgb(0x89b4fa),
        MatchForeground = Rgb(0x11111b),
        MatchBackground = Rgb(0xf9e2af),
        Lanes = Rgbs(0x89b4fa, 0xa6e3a1, 0xf9e2af, 0xcba6f7, 0xf38ba8, 0x94e2d5, 0xfab387, 0xf5c2e7, 0x74c7ec, 0xb4befe, 0xeba0ac, 0xf2cdcd),
    };

    private static PaletteTable TrueColorLight() => new()
    {
        Accent = Rgb(0x1a59d7),
        Good = Rgb(0x2c6f1e),
        Bad = Rgb(0xc30d35),
        Remote = Rgb(0x7f35e0),
        Warn = Rgb(0x8a5811),
        Info = Rgb(0x116d72),
        Muted = Rgb(0x5f6175),
        MutedOnSelection = Rgb(0x5f6175),
        Border = Rgb(0x7c7f93),
        BarBackground = Rgb(0xe6e9ef),
        PanelBackground = Rgb(0xe6e9ef),
        SelectionFocused = Rgb(0xdce0e8),
        SelectionDim = Rgb(0xe9ecf1),
        GroupBackground = Rgb(0xe3e6ec),
        TabButtonBackground = Rgb(0xccd0da),
        ChipForeground = Rgb(0xeff1f5),
        ChipBackground = Rgb(0x1a59d7),
        MatchForeground = Rgb(0x232634),
        MatchBackground = Rgb(0xf2c97a),
        Lanes = Rgbs(0x1a59d7, 0x2c6f1e, 0x8a5811, 0x7f35e0, 0xc30d35, 0x116d72, 0xa74107, 0x91497d, 0x156c7b, 0x4d5bac, 0xb1353f, 0x914f4f),
    };

    // The 16 named colours: the terminal's theme maps them, so they stay readable whatever it is. The selection uses
    // bright black on a dark terminal and the light grey on a light one; the unfocused selection is underlined, since
    // no second background is safe on every theme.
    private static PaletteTable Ansi16Dark() => new()
    {
        Accent = Named(ColorName16.BrightBlue),
        Good = Named(ColorName16.BrightGreen),
        Bad = Named(ColorName16.BrightRed),
        Remote = Named(ColorName16.BrightMagenta),
        Warn = Named(ColorName16.BrightYellow),
        Info = Named(ColorName16.BrightCyan),
        Muted = Named(ColorName16.DarkGray),
        MutedOnSelection = Named(ColorName16.Gray),
        Border = Named(ColorName16.DarkGray),
        BarBackground = Color.None,
        PanelBackground = Color.None,
        SelectionFocused = Named(ColorName16.DarkGray),
        SelectionDim = Color.None,
        GroupBackground = Color.None,
        TabButtonBackground = Color.None,
        ChipForeground = Named(ColorName16.Black),
        ChipBackground = Named(ColorName16.BrightBlue),
        MatchForeground = Named(ColorName16.Black),
        MatchBackground = Named(ColorName16.BrightYellow),
        Lanes = Named(ColorName16.BrightBlue, ColorName16.BrightGreen, ColorName16.BrightYellow, ColorName16.BrightMagenta, ColorName16.BrightRed, ColorName16.BrightCyan),
        SelectionFocusedStyle = TextStyle.Bold,
        SelectionDimStyle = TextStyle.Underline,
    };

    private static PaletteTable Ansi16Light() => new()
    {
        Accent = Named(ColorName16.Blue),
        Good = Named(ColorName16.Green),
        Bad = Named(ColorName16.Red),
        Remote = Named(ColorName16.Blue),
        Warn = Named(ColorName16.Magenta),
        Info = Named(ColorName16.Cyan),
        Muted = Named(ColorName16.DarkGray),
        MutedOnSelection = Named(ColorName16.DarkGray),
        Border = Named(ColorName16.DarkGray),
        BarBackground = Color.None,
        PanelBackground = Color.None,
        SelectionFocused = Named(ColorName16.Gray),
        SelectionDim = Color.None,
        GroupBackground = Color.None,
        TabButtonBackground = Color.None,
        ChipForeground = Named(ColorName16.White),
        ChipBackground = Named(ColorName16.Blue),
        MatchForeground = Named(ColorName16.Black),
        MatchBackground = Named(ColorName16.BrightYellow),
        Lanes = Named(ColorName16.Blue, ColorName16.Green, ColorName16.Magenta, ColorName16.Red, ColorName16.Cyan, ColorName16.DarkGray),
        SelectionFocusedStyle = TextStyle.Bold,
        SelectionDimStyle = TextStyle.Underline,
    };

    // NO_COLOR or a dumb terminal: styles only.
    private static PaletteTable Monochrome() => new()
    {
        Accent = Color.None,
        Good = Color.None,
        Bad = Color.None,
        Remote = Color.None,
        Warn = Color.None,
        Info = Color.None,
        Muted = Color.None,
        MutedOnSelection = Color.None,
        Border = Color.None,
        BarBackground = Color.None,
        PanelBackground = Color.None,
        SelectionFocused = Color.None,
        SelectionDim = Color.None,
        GroupBackground = Color.None,
        TabButtonBackground = Color.None,
        ChipForeground = Color.None,
        ChipBackground = Color.None,
        MatchForeground = Color.None,
        MatchBackground = Color.None,
        Lanes = [Color.None],
        SelectionFocusedStyle = TextStyle.Reverse,
        SelectionDimStyle = TextStyle.Underline,
        MutedStyle = TextStyle.Faint,
        ChipStyle = TextStyle.Bold | TextStyle.Reverse,
        MatchStyle = TextStyle.Bold | TextStyle.Underline,
        CurrentStyle = TextStyle.Underline,
    };

    private static Color Rgb(int value) => new(value >> 16 & 0xff, value >> 8 & 0xff, value & 0xff);

    private static Color[] Rgbs(params int[] values) => [.. values.Select(Rgb)];

    private static Color Named(ColorName16 name) => new(name);

    private static Color[] Named(params ColorName16[] names) => [.. names.Select(Named)];
}
