using Gitunia.Core.Config;
using Gitunia.App;
using Gitunia.App.Status;
using Gitunia.Core.ExternalTools;
using Gitunia.Core.Settings;
using Gitunia.Tui.App.ExternalTools;
using Gitunia.Tui.App.Settings;

namespace Gitunia.Tui.App.Shell;

/// <summary>The shell effects that hand the terminal to an external program.</summary>
/// <param name="tools">Runs the git tools.</param>
/// <param name="editor">Opens the config file in the editor.</param>
/// <param name="writer">Knows the config file.</param>
/// <param name="statuses">Reports a tool that cannot start.</param>
/// <param name="host">Told when the editor closes.</param>
public sealed class ToolEffects(
    ExternalToolRunner tools,
    ConfigEditorLauncher editor,
    ConfigWriter writer,
    StatusReporter statuses,
    Dispatcher host)
{
    /// <summary>Suspends the UI, runs the git tool on the real terminal and refreshes the tab afterwards.</summary>
    /// <param name="effect">The tool.</param>
    public void Launch(LaunchExternalToolEffect effect)
    {
        var notice = tools.Launch(effect.Kind, effect.Location, effect.Commit);
        if (notice.Length > 0)
        {
            statuses.Warning(notice);
        }
    }

    /// <summary>Opens the config file in the editor; the settings screen is told when it closes.</summary>
    public void OpenConfigEditor()
    {
        var notice = editor.Launch(writer.FilePath, () => host.Dispatch(new SettingsEditorClosed()));
        if (notice.Length > 0)
        {
            statuses.Warning(notice);
        }
    }
}
