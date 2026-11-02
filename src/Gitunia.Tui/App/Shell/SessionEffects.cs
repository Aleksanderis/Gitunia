using Gitunia.App;
using Gitunia.Core.Config.Models;

namespace Gitunia.Tui.App.Shell;

/// <summary>The shell effects of the program itself: saving the session, quitting, taking new options and themes.</summary>
/// <param name="persistence">Saves the session.</param>
/// <param name="terminal">Stops the main loop.</param>
/// <param name="applier">Takes new options and themes.</param>
public sealed class SessionEffects(
    SessionPersistence persistence,
    TerminalHost terminal,
    OptionsApplier applier) : ISessionEffects
{
    /// <summary>Saves the session now; false when the save failed.</summary>
    public bool FlushSession() => persistence.FlushSucceeded();

    /// <summary>Stops the program: the main loop ends, the session is saved on the way out.</summary>
    public void Quit() => terminal.RequestStop();

    /// <summary>Makes the options of the config file take effect in the running app.</summary>
    /// <param name="options">The options.</param>
    public void ApplyOptions(ConfigOptions options) => applier.ApplyOptions(options);

    /// <summary>Shows a theme in the running app.</summary>
    /// <param name="theme">The theme.</param>
    public void ApplyTheme(ThemeChoice theme) => applier.ApplyTheme(theme);
}
