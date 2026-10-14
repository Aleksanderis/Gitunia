using Terminal.Gui.Drawing;

namespace Gitunia.Tui.Views;

/// <summary>The look of a segment role before selection is applied.</summary>
/// <param name="Foreground">Text colour.</param>
/// <param name="Background">Background colour, <c>Color.None</c> for the terminal default.</param>
/// <param name="Style">Bold, underline and similar attributes.</param>
/// <param name="KeepsBackground">The background stays when the row is selected (chips, search matches).</param>
public readonly record struct RoleAppearance(Color Foreground, Color Background, TextStyle Style, bool KeepsBackground);
