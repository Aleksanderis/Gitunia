namespace Gitunia.Core.Keymap;

/// <summary>A group of related actions: the subheader the palette and the row menu list them under.</summary>
/// <param name="Header">The subheader text.</param>
/// <param name="Order">Where the group comes among the others (ascending).</param>
/// <param name="WhichKeyName">
/// How the which-key popup names the same group of chords; null when no chord prefix is dedicated to it.
/// </param>
public sealed record ActionGroup(string Header, int Order, string? WhichKeyName = null);
