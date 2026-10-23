namespace Gitunia.Core.Themes.Models;

/// <summary>
/// One theme as read from a theme file: the 24-bit colours of every role, the graph lanes and the optional 16-colour
/// overrides. Plain data, so the Core knows nothing about the toolkit; the rendering layer turns it into a palette.
/// </summary>
/// <param name="Name">The name written in the file (shown to the user).</param>
/// <param name="IsLight">The theme is for a light background (darker accents, which also picks the 16-colour defaults).</param>
/// <param name="Colors">Role key (see <see cref="ThemeRoles"/>) to colour as 0xRRGGBB; <c>groupBackground</c> may be absent.</param>
/// <param name="Lanes">The colours graph lanes cycle through, as 0xRRGGBB.</param>
/// <param name="Ansi">Role key to a 16-colour name (see <see cref="AnsiColorNames"/>) for the roles the theme maps itself.</param>
public sealed record ThemeDefinition(
    string Name,
    bool IsLight,
    IReadOnlyDictionary<string, int> Colors,
    IReadOnlyList<int> Lanes,
    IReadOnlyDictionary<string, string> Ansi);
