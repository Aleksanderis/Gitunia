using Gitunia.Core.Config.Models;
using Gitunia.Core.Settings.Models;

namespace Gitunia.Core.Settings;

/// <summary>File work for the settings screen, run off the UI thread. Each posts one result
/// <see cref="SettingsCommand"/>.</summary>
public abstract record SettingsEffect : AppEffect
{
    private protected SettingsEffect()
    {
    }
}

/// <summary>Read and parse the config file. Posts <see cref="SettingsFileRead"/>.</summary>
/// <param name="Intent">Why it is read.</param>
public sealed record ReadSettingsEffect(SettingsReadIntent Intent) : SettingsEffect;

/// <summary>Write one setting into the config file. Posts <see cref="SettingsWritten"/>.</summary>
/// <param name="Commit">What to write.</param>
/// <param name="Json">The new value as JSON.</param>
/// <param name="Snapshot">The file as last read, so a changed file is not overwritten.</param>
public sealed record WriteSettingEffect(SettingsCommit Commit, string Json, ConfigSnapshot Snapshot)
    : SettingsEffect;

/// <summary>Check on disk that a folder exists. Posts <see cref="SettingsFolderChecked"/>.</summary>
/// <param name="Check">The folder and the change to write when it exists.</param>
public sealed record CheckSettingsFolderEffect(SettingsFolderCheck Check) : SettingsEffect;

/// <summary>What only the terminal layer can do for the settings screen: change the running app, open the
/// editor.</summary>
public abstract record SettingsShellEffect : AppEffect
{
    private protected SettingsShellEffect()
    {
    }
}

/// <summary>The options of the file take effect in the running app.</summary>
/// <param name="Options">The options.</param>
public sealed record ApplySettingsOptionsEffect(ConfigOptions Options) : SettingsShellEffect;

/// <summary>Show a theme in the running app (the one under the cursor, else the kept one).</summary>
/// <param name="Theme">The theme.</param>
public sealed record ApplySettingsThemeEffect(ThemeChoice Theme) : SettingsShellEffect;

/// <summary>Open the config file in the user's editor; <see cref="SettingsEditorClosed"/> follows when it
/// closes.</summary>
public sealed record OpenConfigEditorEffect : SettingsShellEffect;
