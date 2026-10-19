using Gitunia.Core.Config.Models;

namespace Gitunia.Core.Settings.Models;

/// <summary>The config file as read from disk together with the settings parsed from it.</summary>
/// <param name="Snapshot">The file, for the stale check of the next write.</param>
/// <param name="Load">The values, warnings and options parsed from it.</param>
public sealed record SettingsRead(ConfigSnapshot Snapshot, SettingsLoad Load);
