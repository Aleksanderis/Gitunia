namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>A clickable chip under the rebase todo editor: its text and the mouse view a click on it goes to.</summary>
/// <param name="Text">What is drawn, such as <c>[p pick]</c>.</param>
/// <param name="View">The mouse view of the chip (see <c>DefaultMouseMap</c>); the map names its action.</param>
public sealed record RebaseEditorChip(string Text, string View);
