using Gitunia.Core.Keymap;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// A clickable key hint of an editor overlay (rebase, conflict): the key, what it does and the mouse view its click
/// is resolved in. It is drawn through <see cref="HintBar"/>, the key in bold and then its description. The key comes
/// from the keymap (<see cref="HintChipRows.WithKeys"/>); a chip without one shows its description only.
/// </summary>
public sealed record HintChip(string Keys, string Description, string View)
{
    /// <summary>A chip whose key is not known yet (see <see cref="HintChipRows.WithKeys"/>).</summary>
    public HintChip(string description, string view)
        : this(string.Empty, description, view)
    {
    }

    /// <summary>The hint as <see cref="HintBar"/> draws it.</summary>
    public HintItem Item => new(Keys, Description);

    /// <summary>The text as drawn, ligature breaks included: the key, a space, the description.</summary>
    public string Text => new HintItem(KeyFormatter.BreakLigatures(Keys), Description).Text;
}
