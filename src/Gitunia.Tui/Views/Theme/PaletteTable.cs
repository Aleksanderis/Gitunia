using Gitunia.Core.Themes.Models;
using Gitunia.Platform;
using Terminal.Gui.Drawing;

namespace Gitunia.Tui.Views.Theme;

/// <summary>
/// The colours of one scheme: a few accents, muted text, the selection tints and the bars. Every colour decision of the UI is made
/// here and in <see cref="Palette"/> (which role uses which of these), so a theme replaces only this table.
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

    /// <summary>
    /// Whitespace marks of the diff: dimmer than <see cref="Muted"/>, about 2:1 to 3.5:1 on the base background.
    /// </summary>
    public required Color Subtle { get; init; }

    /// <summary>The colour whitespace marks get on a selected row.</summary>
    public required Color SubtleOnSelection { get; init; }

    /// <summary>Background of changed words in an added line; <see cref="Color.None"/> if a style marks them.</summary>
    public required Color AddedWord { get; init; }

    /// <summary>Background of changed words in a removed line; <see cref="Color.None"/> if a style marks it.</summary>
    public required Color RemovedWord { get; init; }

    /// <summary>Lines between panes.</summary>
    public required Color Border { get; init; }

    /// <summary>The header and status bar, <see cref="Color.None"/> for none.</summary>
    public required Color BarBackground { get; init; }

    /// <summary>The selected row of the focused pane.</summary>
    public required Color SelectionFocused { get; init; }

    /// <summary>The selected row of a pane without focus: visible but quieter.</summary>
    public required Color SelectionDim { get; init; }

    /// <summary>
    /// The background of the rows of a Visual range (#814): <see cref="SelectionFocused"/> moved a step towards the accent. Text that
    /// would fall below the readable contrast on it is lightened or darkened per segment (<see cref="EnsureReadable"/>).
    /// <see cref="Color.None"/> where a style carries the range (16 colours, styles only).
    /// </summary>
    public Color RangeBackground { get; init; } = Color.None;

    /// <summary>
    /// Text colour of every segment on a focused selected row where the roles' own colours do not read on
    /// <see cref="SelectionFocused"/> (the classic console, #915); <see cref="Color.None"/> keeps the roles' colours.
    /// </summary>
    public Color SelectionForeground { get; init; } = Color.None;

    /// <summary>The tint behind the change group under the cursor of the working-tree diff, quieter than the selection; <see cref="Color.None"/> where the bar beside the group is the only mark.</summary>
    public required Color GroupBackground { get; init; }

    /// <summary>The background of an inactive repository tab (a button on the bar), visibly apart from <see cref="BarBackground"/> (#240).</summary>
    public required Color TabButtonBackground { get; init; }

    /// <summary>
    /// The background of the selected bottom-pane tab (Details, Files); <see cref="Color.None"/> falls back to
    /// <see cref="SelectionFocused"/>. Kept apart from the bar so the tab keeps its look when the bar gets stronger (#934).
    /// </summary>
    public Color SelectedTabBackground { get; init; } = Color.None;

    /// <summary>Text on a <see cref="ChipBackground"/> (status mode, active tab, overlay title).</summary>
    public required Color ChipForeground { get; init; }

    /// <summary>Filled labels in the accent colour.</summary>
    public required Color ChipBackground { get; init; }

    /// <summary>Text of a search match.</summary>
    public required Color MatchForeground { get; init; }

    /// <summary>Background of a search match.</summary>
    public required Color MatchBackground { get; init; }

    /// <summary>Text colour of the whole interface where a theme is forced against the terminal's own background, <see cref="Color.None"/> for the terminal default.</summary>
    public Color BaseForeground { get; init; } = Color.None;

    /// <summary>Background of the whole interface where a theme is forced against the terminal's own background, <see cref="Color.None"/> for the terminal default.</summary>
    public Color BaseBackground { get; init; } = Color.None;

    /// <summary>The colours graph lanes cycle through.</summary>
    public required Color[] Lanes { get; init; }

    /// <summary>
    /// Text on a branch label of the graph, which is a chip in the colour of its branch: one entry per entry of
    /// <see cref="Lanes"/>, readable on it. Null where it cannot be worked out from the colours (16 colours,
    /// styles only): the label then uses <see cref="ChipForeground"/>.
    /// </summary>
    public Color[]? LaneLabelForegrounds { get; init; }

    /// <summary>Added to selected text of the focused pane (bold where the tint alone is too faint, reverse without colour).</summary>
    public TextStyle SelectionFocusedStyle { get; init; } = TextStyle.None;

    /// <summary>Added to selected text of a pane without focus.</summary>
    public TextStyle SelectionDimStyle { get; init; } = TextStyle.None;

    /// <summary>
    /// Added to the text of a Visual range, so a range never relies on its tint alone: bold and reverse where there is no tint
    /// to tell the range from the selected row.
    /// </summary>
    public TextStyle RangeStyle { get; init; } = TextStyle.Bold;

    /// <summary>Added to the moving end of a Visual range (#823), in every colour tier, so the cursor row differs from the other range rows without colour.</summary>
    public TextStyle RangeCursorStyle { get; init; } = TextStyle.Underline;

    /// <summary>Added to muted text (faint where there is no colour to mute it with).</summary>
    public TextStyle MutedStyle { get; init; } = TextStyle.None;

    /// <summary>Added to whitespace marks (faint at 16 colours, where the named colours leave no dimmer one).</summary>
    public TextStyle SubtleStyle { get; init; } = TextStyle.None;

    /// <summary>Added to changed words (bold; underlined where no background tint can mark them).</summary>
    public TextStyle WordStyle { get; init; } = TextStyle.Bold;

    /// <summary>Added to search matches (bold and underlined where there is no colour to mark them with).</summary>
    public TextStyle MatchStyle { get; init; } = TextStyle.None;

    /// <summary>Added to the current branch (underlined where there is no colour to tell it from other branches).</summary>
    public TextStyle CurrentStyle { get; init; } = TextStyle.None;

    /// <summary>Added to chips (bold, reverse without colour).</summary>
    public TextStyle ChipStyle { get; init; } = TextStyle.Bold;

    /// <summary>The contrast text needs on its background.</summary>
    public const double ReadableContrast = 4.5;

    /// <summary>The contrast whitespace marks keep on a tinted row: faint, but still seen.</summary>
    public const double SubtleContrast = 3.0;

    // How far the range tint of a theme file moves from its selection tint towards its accent.
    private const double LightnessMiddle = 0.18;
    private const double ReadableStep = 0.05;
    private const double RangeAccentShare = 0.2;

    // A light selection bar is darker than its page, so the same share would sink text below the readable ratio.
    private const double RangeAccentShareLight = 0.08;

    // Shared by every table and never changed.
    private static readonly Color[] DarkLanes = Rgbs(
        0x89b4fa, 0xa6e3a1, 0xf9e2af, 0xcba6f7, 0xf38ba8, 0x94e2d5,
        0xfab387, 0xf5c2e7, 0x74c7ec, 0xb4befe, 0xeba0ac, 0xf2cdcd);

    private static readonly Color[] LightLanes = Rgbs(
        0x1a59d7, 0x2c6f1e, 0x8a5811, 0x7f35e0, 0xc30d35, 0x116d72,
        0xa74107, 0x91497d, 0x156c7b, 0x4d5bac, 0xb1353f, 0x914f4f);

    private static readonly Dictionary<string, ColorName16> AnsiNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = ColorName16.Black,
        ["red"] = ColorName16.Red,
        ["green"] = ColorName16.Green,
        ["yellow"] = ColorName16.Yellow,
        ["blue"] = ColorName16.Blue,
        ["magenta"] = ColorName16.Magenta,
        ["cyan"] = ColorName16.Cyan,
        ["gray"] = ColorName16.Gray,
        ["darkGray"] = ColorName16.DarkGray,
        ["brightRed"] = ColorName16.BrightRed,
        ["brightGreen"] = ColorName16.BrightGreen,
        ["brightYellow"] = ColorName16.BrightYellow,
        ["brightBlue"] = ColorName16.BrightBlue,
        ["brightMagenta"] = ColorName16.BrightMagenta,
        ["brightCyan"] = ColorName16.BrightCyan,
        ["white"] = ColorName16.White,
    };

    /// <summary>The table for a terminal: a Catppuccin 24-bit set (Mocha dark, Latte light), the 16 named colours, or styles only.</summary>
    public static PaletteTable For(PaletteSettings settings) => settings.Depth switch
    {
        ColorDepth.None => Monochrome(),
        ColorDepth.Ansi16 => Ansi16For(settings),
        _ when settings.Definition is { } definition => FromDefinition(definition),
        _ => settings.IsLightBackground ? TrueColorLight(settings.PaintsBase) : TrueColorDark(settings.PaintsBase),
    };

    private static PaletteTable Ansi16For(PaletteSettings settings)
    {
        if (settings.IsLightBackground)
        {
            return Ansi16Light(settings.PaintsBase, settings.Definition?.Ansi);
        }

        return Ansi16Dark(settings.PaintsBase, settings.Definition?.Ansi, settings.IsClassicConsole);
    }

    // Catppuccin Mocha (dark) and Latte (light), https://catppuccin.com/palette (MIT licensed colour values, see docs/decisions.md).
    private static PaletteTable TrueColorDark(bool paintsBase) => new()
    {
        Accent = Rgb(0x89b4fa),
        Good = Rgb(0xa6e3a1),
        Bad = Rgb(0xf38ba8),
        Remote = Rgb(0xcba6f7),
        Warn = Rgb(0xf9e2af),
        Info = Rgb(0x89dceb),
        Muted = Rgb(0x9399b2),
        MutedOnSelection = Rgb(0xbac2de),
        Subtle = Rgb(0x5e6277),
        SubtleOnSelection = Rgb(0x8388a0),
        AddedWord = Rgb(0x323c3f),
        RemovedWord = Rgb(0x3e2e40),
        Border = Rgb(0x6c7086),
        BarBackground = Rgb(0x181825),
        SelectionFocused = Rgb(0x45475a),
        SelectionDim = Rgb(0x262637),
        GroupBackground = Rgb(0x222232),
        TabButtonBackground = Rgb(0x313244),
        SelectedTabBackground = Rgb(0x313244),
        ChipForeground = Rgb(0x11111b),
        ChipBackground = Rgb(0x89b4fa),
        MatchForeground = Rgb(0x11111b),
        MatchBackground = Rgb(0xf9e2af),
        BaseForeground = paintsBase ? Rgb(0xcdd6f4) : Color.None,
        BaseBackground = paintsBase ? Rgb(0x1e1e2e) : Color.None,
        RangeBackground = Blend(Rgb(0x45475a), Rgb(0x89b4fa), RangeAccentShare),
        Lanes = DarkLanes,
        LaneLabelForegrounds = ReadableOn(DarkLanes, Rgb(0x11111b)),
    };

    private static PaletteTable TrueColorLight(bool paintsBase) => new()
    {
        Accent = Rgb(0x1a59d7),
        Good = Rgb(0x2c6f1e),
        Bad = Rgb(0xc30d35),
        Remote = Rgb(0x7f35e0),
        Warn = Rgb(0x8a5811),
        Info = Rgb(0x116d72),
        Muted = Rgb(0x5f6175),
        MutedOnSelection = Rgb(0x535670),
        Subtle = Rgb(0xa0a2af),
        SubtleOnSelection = Rgb(0x848798),
        AddedWord = Rgb(0xdde5e2),
        RemovedWord = Rgb(0xece1e8),
        Border = Rgb(0x7c7f93),
        BarBackground = Rgb(0xe6e9ef),
        SelectionFocused = Rgb(0xc9ced9),
        SelectionDim = Rgb(0xe9ecf1),
        GroupBackground = Rgb(0xebedf2),
        TabButtonBackground = Rgb(0xccd0da),
        SelectedTabBackground = Rgb(0xdce0e8),
        ChipForeground = Rgb(0xeff1f5),
        ChipBackground = Rgb(0x1a59d7),
        MatchForeground = Rgb(0x232634),
        MatchBackground = Rgb(0xf2c97a),
        BaseForeground = paintsBase ? Rgb(0x4c4f69) : Color.None,
        BaseBackground = paintsBase ? Rgb(0xeff1f5) : Color.None,
        RangeBackground = Blend(Rgb(0xc9ced9), Rgb(0x1a59d7), RangeAccentShareLight),
        Lanes = LightLanes,
        LaneLabelForegrounds = ReadableOn(LightLanes, Rgb(0xeff1f5)),
    };

    // The 16 named colours: the terminal's theme maps them, so they stay readable whatever it is. The selection uses
    // bright black on a dark terminal and the light grey on a light one; the unfocused selection is underlined, since
    // no second background is safe on every theme.
    private static PaletteTable Ansi16Dark(bool paintsBase, IReadOnlyDictionary<string, string>? ansi, bool classicConsole) => new()
    {
        Accent = Named(ansi, ThemeRoles.Accent, classicConsole ? ColorName16.BrightCyan : ColorName16.BrightBlue),
        Good = Named(ansi, ThemeRoles.Good, ColorName16.BrightGreen),
        Bad = Named(ansi, ThemeRoles.Bad, ColorName16.BrightRed),
        Remote = Named(ansi, ThemeRoles.Remote, ColorName16.BrightMagenta),
        Warn = Named(ansi, ThemeRoles.Warn, ColorName16.BrightYellow),
        Info = Named(ansi, ThemeRoles.Info, ColorName16.BrightCyan),
        Muted = Named(ansi, ThemeRoles.Muted, classicConsole ? ColorName16.Gray : ColorName16.DarkGray),
        MutedOnSelection = Named(
            ansi,
            ThemeRoles.MutedOnSelection,
            classicConsole ? ColorName16.White : ColorName16.Gray),
        Subtle = Named(ansi, ThemeRoles.Muted, ColorName16.DarkGray),
        SubtleOnSelection = Named(ansi, ThemeRoles.MutedOnSelection, ColorName16.Gray),
        SubtleStyle = TextStyle.Faint,
        AddedWord = Color.None,
        RemovedWord = Color.None,
        WordStyle = TextStyle.Bold | TextStyle.Underline,
        Border = Named(ansi, ThemeRoles.Border, ColorName16.DarkGray),
        BarBackground = Color.None,
        SelectionFocused = Named(classicConsole ? ColorName16.Blue : ColorName16.DarkGray),
        SelectionForeground = classicConsole ? Named(ColorName16.White) : Color.None,
        SelectionDim = Color.None,
        GroupBackground = Color.None,
        TabButtonBackground = Color.None,
        ChipForeground = Named(ColorName16.Black),
        ChipBackground = Named(ansi, ThemeRoles.ChipBackground, classicConsole ? ColorName16.BrightCyan : ColorName16.BrightBlue),
        MatchForeground = Named(ColorName16.Black),
        MatchBackground = Named(ColorName16.BrightYellow),
        BaseForeground = BaseText16(paintsBase, classicConsole),
        BaseBackground = paintsBase ? Named(ColorName16.Black) : Color.None,
        Lanes = Named(ColorName16.BrightBlue, ColorName16.BrightGreen, ColorName16.BrightYellow, ColorName16.BrightMagenta, ColorName16.BrightRed, ColorName16.BrightCyan),
        SelectionFocusedStyle = TextStyle.Bold,
        SelectionDimStyle = TextStyle.Underline,
        RangeStyle = TextStyle.Bold | TextStyle.Reverse,
    };

    // The classic console always paints white text, so muted grey stays apart from it; elsewhere the terminal's own.
    private static Color BaseText16(bool paintsBase, bool classicConsole)
    {
        if (classicConsole)
        {
            return Named(ColorName16.White);
        }

        return paintsBase ? Named(ColorName16.Gray) : Color.None;
    }

    private static PaletteTable Ansi16Light(bool paintsBase, IReadOnlyDictionary<string, string>? ansi) => new()
    {
        Accent = Named(ansi, ThemeRoles.Accent, ColorName16.Blue),
        Good = Named(ansi, ThemeRoles.Good, ColorName16.Green),
        Bad = Named(ansi, ThemeRoles.Bad, ColorName16.Red),
        Remote = Named(ansi, ThemeRoles.Remote, ColorName16.Blue),
        Warn = Named(ansi, ThemeRoles.Warn, ColorName16.Magenta),
        Info = Named(ansi, ThemeRoles.Info, ColorName16.Cyan),
        Muted = Named(ansi, ThemeRoles.Muted, ColorName16.DarkGray),
        MutedOnSelection = Named(ansi, ThemeRoles.MutedOnSelection, ColorName16.DarkGray),
        Subtle = Named(ansi, ThemeRoles.Muted, ColorName16.DarkGray),
        SubtleOnSelection = Named(ansi, ThemeRoles.MutedOnSelection, ColorName16.DarkGray),
        SubtleStyle = TextStyle.Faint,
        AddedWord = Color.None,
        RemovedWord = Color.None,
        WordStyle = TextStyle.Bold | TextStyle.Underline,
        Border = Named(ansi, ThemeRoles.Border, ColorName16.DarkGray),
        BarBackground = Color.None,
        SelectionFocused = Named(ColorName16.Gray),
        SelectionDim = Color.None,
        GroupBackground = Color.None,
        TabButtonBackground = Color.None,
        ChipForeground = Named(ColorName16.White),
        ChipBackground = Named(ansi, ThemeRoles.ChipBackground, ColorName16.Blue),
        MatchForeground = Named(ColorName16.Black),
        MatchBackground = Named(ColorName16.BrightYellow),
        BaseForeground = paintsBase ? Named(ColorName16.Black) : Color.None,
        BaseBackground = paintsBase ? Named(ColorName16.White) : Color.None,
        Lanes = Named(ColorName16.Blue, ColorName16.Green, ColorName16.Magenta, ColorName16.Red, ColorName16.Cyan, ColorName16.DarkGray),
        SelectionFocusedStyle = TextStyle.Bold,
        SelectionDimStyle = TextStyle.Underline,
        RangeStyle = TextStyle.Bold | TextStyle.Reverse,
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
        Subtle = Color.None,
        SubtleOnSelection = Color.None,
        AddedWord = Color.None,
        RemovedWord = Color.None,
        WordStyle = TextStyle.Bold | TextStyle.Underline,
        Border = Color.None,
        BarBackground = Color.None,
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
        RangeStyle = TextStyle.Bold | TextStyle.Reverse,
        MutedStyle = TextStyle.Faint,
        SubtleStyle = TextStyle.Faint,
        ChipStyle = TextStyle.Bold | TextStyle.Reverse,
        MatchStyle = TextStyle.Bold | TextStyle.Underline,
        CurrentStyle = TextStyle.Underline,
    };

    // A theme file: the colours as written. The base colours are always painted, so the theme looks the same on any
    // terminal background.
    private static PaletteTable FromDefinition(ThemeDefinition definition) => new()
    {
        Accent = Role(definition, ThemeRoles.Accent),
        Good = Role(definition, ThemeRoles.Good),
        Bad = Role(definition, ThemeRoles.Bad),
        Remote = Role(definition, ThemeRoles.Remote),
        Warn = Role(definition, ThemeRoles.Warn),
        Info = Role(definition, ThemeRoles.Info),
        Muted = Role(definition, ThemeRoles.Muted),
        MutedOnSelection = Role(definition, ThemeRoles.MutedOnSelection),
        Subtle = Role(definition, ThemeRoles.Subtle),
        SubtleOnSelection = Role(definition, ThemeRoles.SubtleOnSelection),
        AddedWord = Role(definition, ThemeRoles.AddedWord),
        RemovedWord = Role(definition, ThemeRoles.RemovedWord),
        Border = Role(definition, ThemeRoles.Border),
        BarBackground = Role(definition, ThemeRoles.BarBackground),
        SelectionFocused = Role(definition, ThemeRoles.SelectionFocused),
        SelectionDim = Role(definition, ThemeRoles.SelectionDim),
        GroupBackground = Role(definition, ThemeRoles.GroupBackground),
        TabButtonBackground = Role(definition, ThemeRoles.TabButtonBackground),
        SelectedTabBackground = Role(definition, ThemeRoles.TabButtonBackground),
        ChipForeground = Role(definition, ThemeRoles.ChipForeground),
        ChipBackground = Role(definition, ThemeRoles.ChipBackground),
        MatchForeground = Role(definition, ThemeRoles.MatchForeground),
        MatchBackground = Role(definition, ThemeRoles.MatchBackground),
        BaseForeground = Role(definition, ThemeRoles.BaseForeground),
        BaseBackground = Role(definition, ThemeRoles.BaseBackground),
        Lanes = [.. definition.Lanes.Select(Rgb)],
        LaneLabelForegrounds = LabelForegrounds(definition),
        RangeBackground = Blend(
            Role(definition, ThemeRoles.SelectionFocused),
            Role(definition, ThemeRoles.Accent),
            definition.IsLight ? RangeAccentShareLight : RangeAccentShare),
    };

    /// <summary>
    /// The foreground moved towards white (on a dark background) or black (on a light one) until it reaches
    /// <paramref name="minimum"/> contrast on <paramref name="background"/>; unchanged when it already does or when either
    /// colour is not a 24-bit one.
    /// </summary>
    public static Color EnsureReadable(Color foreground, Color background, double minimum)
    {
        if (foreground == Color.None || background == Color.None || ContrastOf(foreground, background) >= minimum)
        {
            return foreground;
        }

        var target = LuminanceOf(background) < LightnessMiddle ? Rgb(0xffffff) : Rgb(0x000000);
        for (var share = ReadableStep; share < 1.0; share += ReadableStep)
        {
            var candidate = Blend(foreground, target, share);
            if (ContrastOf(candidate, background) >= minimum)
            {
                return candidate;
            }
        }

        return target;
    }

    // A step from one colour towards another; the second one missing leaves the first.
    private static Color Blend(Color from, Color to, double share)
    {
        if (from == Color.None || to == Color.None)
        {
            return from;
        }

        return new Color(
            (int)Math.Round(from.R + ((to.R - from.R) * share)),
            (int)Math.Round(from.G + ((to.G - from.G) * share)),
            (int)Math.Round(from.B + ((to.B - from.B) * share)));
    }

    private static Color[] LabelForegrounds(ThemeDefinition definition) =>
        ReadableOn([.. definition.Lanes.Select(Rgb)], Role(definition, ThemeRoles.ChipForeground));

    private static Color Role(ThemeDefinition definition, string role) =>
        definition.Colors.TryGetValue(role, out var value) ? Rgb(value) : Color.None;

    // For every background the preferred foreground when readable (4.5) on it, else the better of black and white.
    private static Color[] ReadableOn(Color[] backgrounds, Color preferred) =>
        [.. backgrounds.Select(background => ReadableOn(background, preferred))];

    private static Color ReadableOn(Color background, Color preferred)
    {
        if (preferred != Color.None && ContrastOf(preferred, background) >= ReadableContrast)
        {
            return preferred;
        }

        var black = Rgb(0x000000);
        var white = Rgb(0xffffff);

        return ContrastOf(black, background) >= ContrastOf(white, background) ? black : white;
    }

    private static double ContrastOf(Color first, Color second)
    {
        var lighter = Math.Max(LuminanceOf(first), LuminanceOf(second));
        var darker = Math.Min(LuminanceOf(first), LuminanceOf(second));

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double LuminanceOf(Color color) =>
        (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    private static double Linear(int channel)
    {
        var value = channel / 255.0;

        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static Color Rgb(int value) => new(value >> 16 & 0xff, value >> 8 & 0xff, value & 0xff);

    private static Color[] Rgbs(params int[] values) => [.. values.Select(Rgb)];

    private static Color Named(ColorName16 name) => new(name);

    // The colour a theme maps a role to in its ansi object, else the default of the variant.
    private static Color Named(IReadOnlyDictionary<string, string>? ansi, string role, ColorName16 fallback)
    {
        if (ansi is not null && ansi.TryGetValue(role, out var text) && AnsiNames.TryGetValue(text, out var name))
        {
            return new Color(name);
        }

        return new Color(fallback);
    }

    private static Color[] Named(params ColorName16[] names) => [.. names.Select(Named)];
}
