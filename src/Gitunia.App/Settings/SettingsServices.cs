using Gitunia.Core.Config;
using Gitunia.Core.Keymap;
using Gitunia.Core.Settings.Models;

namespace Gitunia.App.Settings;

/// <summary>What the settings file work needs: the config file, the machine facts, the settings on offer and the
/// running key bindings (for the key reference).</summary>
/// <param name="Writer">Reads and edits the config file.</param>
/// <param name="Environment">The home folder and the folder check the parser needs.</param>
/// <param name="Definitions">The settings on offer.</param>
/// <param name="Keymap">The key binding table of the running app.</param>
public sealed record SettingsServices(
    ConfigWriter Writer,
    SettingsEnvironment Environment,
    IReadOnlyList<SettingDefinition> Definitions,
    KeyBindingTable Keymap);
