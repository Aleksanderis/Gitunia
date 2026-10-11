namespace Gitunia.Tui.Rendering;

/// <summary>The characters the graph is drawn with; there is a box-drawing set and an ASCII fallback.</summary>
/// <param name="Node">A commit.</param>
/// <param name="MergeNode">A commit with several parents.</param>
/// <param name="Lines">Line characters indexed by <see cref="LineDirections"/> flags (16 entries).</param>
/// <param name="Ellipsis">Shown where text was cut.</param>
public sealed record GraphGlyphSet(char Node, char MergeNode, string Lines, string Ellipsis)
{
    // Index: Up=1, Down=2, Left=4, Right=8. Lone strokes borrow the straight line of their axis.
    public static GraphGlyphSet Unicode { get; } = new('●', '○', " │││─╯╮┤─╰╭├─┴┬┼", "…");

    public static GraphGlyphSet Ascii { get; } = new('*', 'o', " |||-/\\|-\\/|---+", "~");

    /// <summary>A horizontal connection.</summary>
    public char Horizontal => Lines[(int)(LineDirections.Left | LineDirections.Right)];

    /// <summary>The character for a cell whose lines go in the given directions.</summary>
    public char For(LineDirections directions) => Lines[(int)directions];

    /// <summary>The set a terminal with the given Unicode support can show.</summary>
    public static GraphGlyphSet For(bool unicode) => unicode ? Unicode : Ascii;
}
