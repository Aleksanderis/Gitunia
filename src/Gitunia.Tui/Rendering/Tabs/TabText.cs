namespace Gitunia.Tui.Rendering.Tabs;

/// <summary>The text of a tab chip and where its dirty marker sits in it.</summary>
/// <param name="Text">The chip text with padding.</param>
/// <param name="DirtyOffset">The column of the <c>*</c> within the text, or null when there is none.</param>
public sealed record TabText(string Text, int? DirtyOffset);
