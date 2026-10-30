namespace Gitunia.Core.Keymap;

/// <summary>A group of related actions: the subheader the palette and the row menu list them under.</summary>
/// <param name="Header">The subheader text.</param>
/// <param name="Order">Where the group comes among the others (ascending).</param>
public sealed record ActionGroup(string Header, int Order);
