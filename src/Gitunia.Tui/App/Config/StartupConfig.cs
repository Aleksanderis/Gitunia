using Gitunia.Core.Config.Models;

namespace Gitunia.Tui.App.Config;

/// <summary>What the config file configures at startup.</summary>
/// <param name="Keymap">The key table and every problem found while loading (keys and settings alike).</param>
/// <param name="Theme">The light or dark choice.</param>
/// <param name="Options">Tab branch and scan roots.</param>
public sealed record StartupConfig(KeymapBuildResult Keymap, ThemeChoice Theme, ConfigOptions Options);
