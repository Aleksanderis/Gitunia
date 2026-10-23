namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>A clickable chip of the conflict editor: its text and the mouse view its click is resolved in.</summary>
public sealed record ConflictEditorChip(string Text, string View);
