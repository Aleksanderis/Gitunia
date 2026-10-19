namespace Gitunia.Core.Settings.Models;

/// <summary>A change to write to the config file.</summary>
/// <param name="Definition">The setting that changes.</param>
/// <param name="Value">Its new value.</param>
/// <param name="SelectItem">The folder the cursor goes to once the change is written, -1 for none.</param>
public sealed record SettingsCommit(SettingDefinition Definition, SettingValue Value, int SelectItem = -1);
