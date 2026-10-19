namespace Gitunia.Core.Settings.Models;

/// <summary>A choice whose options are listed under its row while the cursor picks one.</summary>
/// <param name="SettingId">The setting being edited.</param>
/// <param name="Original">The option that was selected when the list opened, restored by <c>Esc</c>.</param>
public sealed record ChoiceEdit(string SettingId, int Original);
