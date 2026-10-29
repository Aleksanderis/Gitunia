
namespace Gitunia.Tui.Rendering.Graph;

/// <summary>The characters the graph is drawn with; there is a box-drawing set and an ASCII fallback.</summary>
/// <param name="Node">A commit.</param>
/// <param name="MergeNode">A commit with several parents.</param>
/// <param name="LineNode">A commit in the middle of a branch: one line in and one out on its own lane, drawn as part of the line.</param>
/// <param name="Lines">Line characters indexed by <see cref="LineDirections"/> flags (16 entries).</param>
/// <param name="Ellipsis">Shown where text was cut.</param>
/// <param name="CurrentBranch">Before the name of the branch HEAD is on, inside its label box.</param>
/// <param name="Remote">After the name, inside the label box (the Unicode glyph keeps a trailing space as padding so the wide icon does not touch the box edge), of a branch that also stands for its remote-tracking branch.</param>
/// <param name="UpstreamGone">After the label of a local branch whose upstream no longer exists.</param>
public sealed record GraphGlyphSet(
    char Node,
    char MergeNode,
    char LineNode,
    string Lines,
    string Ellipsis,
    string CurrentBranch,
    string Remote,
    string UpstreamGone)
{
    // Index: Up=1, Down=2, Left=4, Right=8. Lone strokes borrow the straight line of their axis.
    public static GraphGlyphSet Unicode { get; } = new('●', '○', '├', " │││─╯╮┤─╰╭├─┴┬┼", "…", "⎇ ", "  ", " ✗");

    public static GraphGlyphSet Ascii { get; } =
        new('*', 'o', '|', " |||-/\\|-\\/|---+", "~", "HEAD -> ", " (r)", " (x)");

    /// <summary>This is the ASCII fallback set.</summary>
    public bool IsAscii => Lines == Ascii.Lines;

    /// <summary>Drawn in place of a tab's first column when whitespace is shown.</summary>
    public char TabMark => IsAscii ? '>' : '→';

    /// <summary>Drawn in place of a trailing space (a heavier mark than inner ones) when whitespace is shown.</summary>
    public char SpaceMark => IsAscii ? '.' : '•';

    /// <summary>Drawn in place of a leading or inner space when whitespace is shown.</summary>
    public char InnerSpaceMark => IsAscii ? '.' : '·';

    /// <summary>Drawn in place of a carriage return when whitespace is shown.</summary>
    public char CarriageReturnMark => IsAscii ? '<' : '¶';

    /// <summary>A horizontal connection.</summary>
    public char Horizontal => Lines[(int)(LineDirections.Left | LineDirections.Right)];

    /// <summary>The character for a cell whose lines go in the given directions.</summary>
    public char For(LineDirections directions) => Lines[(int)directions];

    /// <summary>The set a terminal with the given Unicode support can show.</summary>
    public static GraphGlyphSet For(bool unicode) => unicode ? Unicode : Ascii;
}
