using Gitunia.Platform;
using Terminal.Gui.Drawing;

namespace Gitunia.Tui.Views;

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

    /// <summary>Removed, remote branches, errors.</summary>
    public required Color Bad { get; init; }

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

    /// <summary>The selected row of the focused pane.</summary>
    public required Color SelectionFocused { get; init; }

    /// <summary>The selected row of a pane without focus: visible but quieter.</summary>
    public required Color SelectionDim { get; init; }

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

    /// <summary>Added to chips (bold, reverse without colour).</summary>
    public TextStyle ChipStyle { get; init; } = TextStyle.Bold;

    /// <summary>The table for a terminal: a GitHub-like 24-bit set (dark and light variants), the 16 named colours, or styles only.</summary>
    public static PaletteTable For(PaletteSettings settings) => settings.Depth switch
    {
        ColorDepth.None => Monochrome(),
        ColorDepth.Ansi16 => settings.IsLightBackground ? Ansi16Light() : Ansi16Dark(),
        _ => settings.IsLightBackground ? TrueColorLight() : TrueColorDark(),
    };

    private static PaletteTable TrueColorDark() => new()
    {
        Accent = Rgb(0x58a6ff),
        Good = Rgb(0x3fb950),
        Bad = Rgb(0xff7b72),
        Warn = Rgb(0xd29922),
        Info = Rgb(0x39c5cf),
        Muted = Rgb(0x8b949e),
        MutedOnSelection = Rgb(0xa5adb7),
        Border = Rgb(0x6e7681),
        BarBackground = Rgb(0x161b22),
        SelectionFocused = Rgb(0x1c2d4a),
        SelectionDim = Rgb(0x242b35),
        ChipForeground = Rgb(0x0d1117),
        ChipBackground = Rgb(0x58a6ff),
        MatchForeground = Rgb(0x0d1117),
        MatchBackground = Rgb(0xe3b341),
        Lanes = Rgbs(0x58a6ff, 0x3fb950, 0xd29922, 0xbc8cff, 0xff7b72, 0x39c5cf, 0xf0883e, 0xf778ba, 0x79c0ff, 0x56d364, 0xe3b341, 0xd2a8ff),
    };

    private static PaletteTable TrueColorLight() => new()
    {
        Accent = Rgb(0x0550ae),
        Good = Rgb(0x116329),
        Bad = Rgb(0xa40e26),
        Warn = Rgb(0x7d4e00),
        Info = Rgb(0x0e6169),
        Muted = Rgb(0x57606a),
        MutedOnSelection = Rgb(0x424a53),
        Border = Rgb(0x7d8590),
        BarBackground = Rgb(0xeaeef2),
        SelectionFocused = Rgb(0xdce9f9),
        SelectionDim = Rgb(0xe8ebef),
        ChipForeground = Rgb(0xffffff),
        ChipBackground = Rgb(0x0550ae),
        MatchForeground = Rgb(0x1f2328),
        MatchBackground = Rgb(0xffd33d),
        Lanes = Rgbs(0x0550ae, 0x116329, 0x7d4e00, 0x6639ba, 0xa40e26, 0x0e6169, 0x953800, 0x99286e, 0x0a3069, 0x0b4f1d, 0x633c01, 0x512a97),
    };

    // The 16 named colours: the terminal's theme maps them, so they stay readable whatever it is. The selection uses
    // bright black on a dark terminal and the light grey on a light one; the unfocused selection is underlined, since
    // no second background is safe on every theme.
    private static PaletteTable Ansi16Dark() => new()
    {
        Accent = Named(ColorName16.BrightBlue),
        Good = Named(ColorName16.BrightGreen),
        Bad = Named(ColorName16.BrightRed),
        Warn = Named(ColorName16.BrightYellow),
        Info = Named(ColorName16.BrightCyan),
        Muted = Named(ColorName16.DarkGray),
        MutedOnSelection = Named(ColorName16.Gray),
        Border = Named(ColorName16.DarkGray),
        BarBackground = Color.None,
        SelectionFocused = Named(ColorName16.DarkGray),
        SelectionDim = Color.None,
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
        Warn = Named(ColorName16.Magenta),
        Info = Named(ColorName16.Cyan),
        Muted = Named(ColorName16.DarkGray),
        MutedOnSelection = Named(ColorName16.DarkGray),
        Border = Named(ColorName16.DarkGray),
        BarBackground = Color.None,
        SelectionFocused = Named(ColorName16.Gray),
        SelectionDim = Color.None,
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
        Warn = Color.None,
        Info = Color.None,
        Muted = Color.None,
        MutedOnSelection = Color.None,
        Border = Color.None,
        BarBackground = Color.None,
        SelectionFocused = Color.None,
        SelectionDim = Color.None,
        ChipForeground = Color.None,
        ChipBackground = Color.None,
        MatchForeground = Color.None,
        MatchBackground = Color.None,
        Lanes = [Color.None],
        SelectionFocusedStyle = TextStyle.Reverse,
        SelectionDimStyle = TextStyle.Underline,
        MutedStyle = TextStyle.Faint,
        ChipStyle = TextStyle.Bold | TextStyle.Reverse,
    };

    private static Color Rgb(int value) => new(value >> 16 & 0xff, value >> 8 & 0xff, value & 0xff);

    private static Color[] Rgbs(params int[] values) => [.. values.Select(Rgb)];

    private static Color Named(ColorName16 name) => new(name);

    private static Color[] Named(params ColorName16[] names) => [.. names.Select(Named)];
}
