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
public sealed record ConsoleCapabilities(bool IsInteractive, ColorDepth Color, bool Unicode);

/// <summary>
/// The only place OS-specific behaviour lives. Every member has an implementation
/// (or an explicit "unsupported" result) for each supported OS.
/// </summary>
public interface IPlatform
{
    /// <summary>Directory for user configuration (keys, theme).</summary>
    string ConfigDir { get; }

    /// <summary>Directory for state (session, recents).</summary>
    string StateDir { get; }

    ConsoleCapabilities DetectConsole();
}
