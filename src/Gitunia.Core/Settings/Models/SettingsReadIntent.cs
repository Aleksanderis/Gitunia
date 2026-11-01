namespace Gitunia.Core.Settings.Models;

/// <summary>Why the config file is read.</summary>
public enum SettingsReadIntent
{
    /// <summary>Open the screen.</summary>
    Open,

    /// <summary>Open the screen with the cursor on the theme row.</summary>
    OpenAtTheme,

    /// <summary>The fast toggle: write the next command level.</summary>
    CycleCommandLevel,

    /// <summary>The editor closed: read the file again.</summary>
    Reload,
}
