namespace Gitunia.Core.Config.Models;

/// <summary>
/// What the user asked for in the <c>theme</c> setting: <c>auto</c> or the name of a theme file (built-in or
/// the user's own); <c>dark</c> and <c>light</c> are older values, still accepted. The name is lower case.
/// </summary>
/// <param name="Name">The lower-case text as written in the file.</param>
public sealed record ThemeChoice(string Name)
{
    /// <summary>The text of <see cref="Auto"/>.</summary>
    public const string AutoName = "auto";

    /// <summary>The settings screen's name for the old <c>dark</c> value.</summary>
    public const string MochaName = "catppuccin-mocha";

    /// <summary>The settings screen's name for the old <c>light</c> value.</summary>
    public const string LatteName = "catppuccin-latte";

    /// <summary>The text of <see cref="Dark"/>.</summary>
    public const string DarkName = "dark";

    /// <summary>The text of <see cref="Light"/>.</summary>
    public const string LightName = "light";

    /// <summary>Follow the terminal: light when it reports a light background, else dark.</summary>
    public static ThemeChoice Auto { get; } = new(AutoName);

    /// <summary>The built-in dark table (older value, close to Catppuccin Mocha).</summary>
    public static ThemeChoice Dark { get; } = new(DarkName);

    /// <summary>The built-in light table (older value, close to Catppuccin Latte).</summary>
    public static ThemeChoice Light { get; } = new(LightName);

    /// <summary>The choice for a text, ignoring case and surrounding spaces.</summary>
    /// <param name="text">The text from the file or a list option.</param>
    public static ThemeChoice FromText(string text) => new(text.Trim().ToLowerInvariant());

    /// <summary>Whether this is a theme file rather than auto, dark or light.</summary>
    public bool IsNamedTheme => Name is not (AutoName or DarkName or LightName);
}
