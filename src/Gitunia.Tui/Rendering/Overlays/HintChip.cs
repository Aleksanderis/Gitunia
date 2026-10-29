using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// A clickable key hint of an editor overlay (rebase, conflict): the key, what it does and the mouse view its click
/// is resolved in. It is drawn through <see cref="HintBar"/>, the key in bold and then its description.
/// </summary>
public sealed record HintChip(string Keys, string Description, string View)
{
    /// <summary>The text as drawn: the key, a space, the description.</summary>
    public string Text => $"{Keys} {Description}";
}
