using Gitunia.Core.Config.Models;
using Gitunia.Core.Themes;

namespace Gitunia.Tui.App.Config;

/// <summary>What the config file configures at startup.</summary>
/// <param name="Keymap">The key table and every problem found while loading (keys and settings alike).</param>
/// <param name="Theme">The theme choice: auto, dark, light or the name of a theme file.</param>
/// <param name="Options">Tab branch and scan roots.</param>
/// <param name="Themes">The themes that can be chosen (built-in and the user's files).</param>
public sealed record StartupConfig(KeymapBuildResult Keymap, ThemeChoice Theme, ConfigOptions Options, ThemeCatalog Themes);
