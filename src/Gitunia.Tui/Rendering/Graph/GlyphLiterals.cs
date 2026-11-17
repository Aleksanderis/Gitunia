namespace Gitunia.Tui.Rendering.Graph;

/// <summary>
/// The Unicode glyphs that renderers use outside a <see cref="GraphGlyphSet"/>, in one place: a lint test (#1449) keeps literal box, arrow,
/// check and cross characters out of every other renderer file, so a glyph has one spelling and an ASCII fallback has one place to be added.
/// </summary>
public static class GlyphLiterals
{
    /// <summary>The marker of a dropdown (the view chips of the panes).</summary>
    public const string Dropdown = "▾";

    /// <summary>The marker of a choice setting (the ASCII form is chosen by the caller).</summary>
    public const string Choice = "▾";

    /// <summary>A chosen option of a list of options.</summary>
    public const string RadioOn = "●";

    /// <summary>An option of a list of options that is not chosen.</summary>
    public const string RadioOff = "○";

    /// <summary>A conflict that is resolved.</summary>
    public const string Check = "✓";

    /// <summary>Commits ahead, on a tab label.</summary>
    public const string Ahead = "↑";

    /// <summary>Commits behind, on a tab label.</summary>
    public const string Behind = "↓";

    /// <summary>The mark of a whitespace tab.</summary>
    public const char TabArrow = '→';
}
