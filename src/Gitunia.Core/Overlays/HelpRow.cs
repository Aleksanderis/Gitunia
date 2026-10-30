using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>One line of the help overlay.</summary>
/// <param name="Keys">The main key of the action (<c>Tab</c>): its first binding.</param>
/// <param name="Description">What the action does.</param>
/// <param name="Also">The other keys of the same action (<c>Ctrl-w w</c>), drawn after the description as "also ...", empty when there are none.</param>
/// <param name="Action">The action the row describes, null for rows that are not an action (markers, mouse paths).</param>
/// <param name="GapBefore">Whether a blank line separates the row from the previous one: it starts a new cluster of related actions.</param>
public sealed record HelpRow(
    string Keys,
    string Description,
    string Also = "",
    ActionId? Action = null,
    bool GapBefore = false)
{
    /// <summary>The description as drawn: the title, then "also" and the other keys.</summary>
    public string Text => Also.Length == 0 ? Description : $"{Description}  (also {Also})";
}
