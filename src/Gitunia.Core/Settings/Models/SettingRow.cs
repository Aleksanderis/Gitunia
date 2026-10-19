namespace Gitunia.Core.Settings.Models;

/// <summary>One row of the settings column.</summary>
/// <param name="Definition">The setting the row belongs to.</param>
/// <param name="Kind">What the row is.</param>
/// <param name="Index">The folder or option number for an item or option row, else 0.</param>
public sealed record SettingRow(SettingDefinition Definition, SettingRowKind Kind, int Index);
