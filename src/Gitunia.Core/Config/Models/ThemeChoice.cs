namespace Gitunia.Core.Config.Models;

/// <summary>Which light or dark scheme the user asked for in the <c>theme</c> setting.</summary>
public enum ThemeChoice
{
    /// <summary>Follow the terminal: light when it reports a light background, else dark.</summary>
    Auto,

    /// <summary>Always the dark scheme.</summary>
    Dark,

    /// <summary>Always the light scheme.</summary>
    Light,
}
