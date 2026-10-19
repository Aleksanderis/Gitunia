namespace Gitunia.Core.Settings.Models;

/// <summary>
/// A choice whose options are listed under its row while the cursor picks one. Nothing is applied until an option is
/// kept, so going back needs no saved original.
/// </summary>
/// <param name="SettingId">The setting being edited.</param>
public sealed record ChoiceEdit(string SettingId);
