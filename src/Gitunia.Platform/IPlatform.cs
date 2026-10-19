namespace Gitunia.Platform;

/// <summary>Terminal colour depth, detected from the environment.</summary>
public enum ColorDepth
{
    None,
    Ansi16,
    Ansi256,
    TrueColor,
}

/// <summary>What the attached terminal can do. Detected, never assumed.</summary>
/// <param name="IsInteractive">Input and output are a terminal.</param>
/// <param name="Color">The colour depth.</param>
/// <param name="Unicode">The terminal can show box-drawing and similar characters.</param>
/// <param name="IsLightBackground">The terminal reports a light background (<c>COLORFGBG</c>); false for dark and for unknown.</param>
public sealed record ConsoleCapabilities(bool IsInteractive, ColorDepth Color, bool Unicode, bool IsLightBackground = false);

/// <summary>
/// The only place OS-specific behaviour lives. Every member has an implementation
/// (or an explicit "unsupported" result) for each supported OS.
/// </summary>
public interface IPlatform
{
    /// <summary>Directory for user configuration (keys, theme).</summary>
    string ConfigDirectory { get; }

    /// <summary>Directory for state (session, recents).</summary>
    string StateDirectory { get; }

    ConsoleCapabilities DetectConsole();

    /// <summary>
    /// Asks the terminal whether its background is light (OSC 11, short timeout), writing to the terminal and reading its answer.
    /// Null when unknown (not interactive, an unsupported OS or terminal, no answer): callers keep
    /// <see cref="ConsoleCapabilities.IsLightBackground"/>. Call before the UI takes over the input.
    /// </summary>
    /// <param name="capabilities">What <see cref="DetectConsole"/> found.</param>
    bool? QueryLightBackground(ConsoleCapabilities capabilities);
}
