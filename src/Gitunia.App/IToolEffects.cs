using Gitunia.Core.ExternalTools;

namespace Gitunia.App;

/// <summary>External tools and the config editor.</summary>
public interface IToolEffects
{
    /// <summary>Opens the config file in the editor; <c>SettingsEditorClosed</c> follows when it closes.</summary>
    void OpenConfigEditor();

    /// <summary>Suspends the UI, runs the git tool on the real terminal and refreshes the tab afterwards.</summary>
    void LaunchExternalTool(LaunchExternalToolEffect effect);
}
