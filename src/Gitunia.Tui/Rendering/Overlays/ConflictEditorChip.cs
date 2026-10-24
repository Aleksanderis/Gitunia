namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// A clickable key hint of the conflict editor: the key, what it does and the mouse view its click is resolved in.
/// It is drawn like the status bar hints, the key in bold and then its description.
/// </summary>
public sealed record ConflictEditorChip(string Keys, string Description, string View)
{
    /// <summary>The text as drawn: the key, a space, the description.</summary>
    public string Text => $"{Keys} {Description}";
}
