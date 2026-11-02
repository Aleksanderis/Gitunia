using Gitunia.Core.Config.Models;

namespace Gitunia.App;

/// <summary>Options, theme, session saving and quit.</summary>
public interface ISessionEffects
{
    /// <summary>Makes the options of the config file take effect in the running app.</summary>
    void ApplyOptions(ConfigOptions options);

    /// <summary>Shows a theme in the running app.</summary>
    void ApplyTheme(ThemeChoice theme);

    /// <summary>Saves the session now; false when the save failed.</summary>
    bool FlushSession();

    /// <summary>Stops the program: the main loop ends, the session is saved on the way out.</summary>
    void Quit();
}
