namespace Gitunia.Core.Themes.Models;

/// <summary>
/// The role keys of a theme file's <c>colors</c> object (and of its <c>ansi</c> object, for the ones in
/// <see cref="AnsiRoles"/>).
/// </summary>
public static class ThemeRoles
{
    /// <summary>Focus, section titles, active tab, buttons.</summary>
    public const string Accent = "accent";

    /// <summary>Added, local branches.</summary>
    public const string Good = "good";

    /// <summary>Removed lines and errors.</summary>
    public const string Bad = "bad";

    /// <summary>Remote branches.</summary>
    public const string Remote = "remote";

    /// <summary>Tags, modified files, overlay keys.</summary>
    public const string Warn = "warn";

    /// <summary>The current branch, diff hunks.</summary>
    public const string Info = "info";

    /// <summary>Hashes, dates and everything secondary.</summary>
    public const string Muted = "muted";

    /// <summary>Muted text on a selected row; defaults to <see cref="Muted"/>.</summary>
    public const string MutedOnSelection = "mutedOnSelection";

    /// <summary>Lines between panes.</summary>
    public const string Border = "border";

    /// <summary>The header and status bar.</summary>
    public const string BarBackground = "barBackground";

    /// <summary>The selected row of the focused pane.</summary>
    public const string SelectionFocused = "selectionFocused";

    /// <summary>The selected row of a pane without focus.</summary>
    public const string SelectionDim = "selectionDim";

    /// <summary>The tint behind the change group under the cursor; optional, absent means no tint.</summary>
    public const string GroupBackground = "groupBackground";

    /// <summary>An inactive repository tab; defaults to <see cref="SelectionFocused"/>.</summary>
    public const string TabButtonBackground = "tabButtonBackground";

    /// <summary>Text on a chip.</summary>
    public const string ChipForeground = "chipForeground";

    /// <summary>Filled labels; defaults to <see cref="Accent"/>.</summary>
    public const string ChipBackground = "chipBackground";

    /// <summary>Text of a search match; defaults to <see cref="ChipForeground"/>.</summary>
    public const string MatchForeground = "matchForeground";

    /// <summary>Background of a search match; defaults to <see cref="Warn"/>.</summary>
    public const string MatchBackground = "matchBackground";

    /// <summary>Text colour of the whole interface.</summary>
    public const string BaseForeground = "baseForeground";

    /// <summary>Background of the whole interface.</summary>
    public const string BaseBackground = "baseBackground";

    /// <summary>The roles a theme file must give.</summary>
    public static IReadOnlyList<string> Required { get; } =
    [
        Accent, Good, Bad, Remote, Warn, Info, Muted, Border, BarBackground, SelectionFocused, SelectionDim,
        ChipForeground, BaseForeground, BaseBackground,
    ];

    /// <summary>The roles a theme file may leave out (the default is given at each key).</summary>
    public static IReadOnlyList<string> Optional { get; } =
    [
        MutedOnSelection, GroupBackground, TabButtonBackground, ChipBackground, MatchForeground, MatchBackground,
    ];

    /// <summary>The roles whose 16-colour name a theme file may set in its <c>ansi</c> object.</summary>
    public static IReadOnlyList<string> AnsiRoles { get; } =
    [
        Accent, Good, Bad, Remote, Warn, Info, Muted, MutedOnSelection, Border, ChipBackground,
    ];
}
