using Gitunia.Core.Config.Models;

namespace Gitunia.Tui.Rendering.Graph;

/// <summary>The characters the graph is drawn with; there is a box-drawing set and an ASCII fallback.</summary>
/// <param name="Node">A commit.</param>
/// <param name="MergeNode">A commit with several parents.</param>
/// <param name="LineNode">A commit in the middle of a branch: one line in and one out on its own lane, drawn as part of the line.</param>
/// <param name="Lines">Line characters indexed by <see cref="LineDirections"/> flags (16 entries).</param>
/// <param name="Ellipsis">Shown where text was cut.</param>
/// <param name="CurrentBranch">Before the label of the branch HEAD is on.</param>
/// <param name="Remote">After a branch label that also stands for its remote-tracking branch.</param>
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
    private const char SquareTopLeft = '┌';
    private const char SquareTopRight = '┐';
    private const char SquareBottomLeft = '└';
    private const char SquareBottomRight = '┘';

    // Index: Up=1, Down=2, Left=4, Right=8. Lone strokes borrow the straight line of their axis.
    public static GraphGlyphSet Unicode { get; } = new('●', '○', '├', " │││─╯╮┤─╰╭├─┴┬┼", "…", "⎇ ", " ", " ✗");

    public static GraphGlyphSet Ascii { get; } =
        new('*', 'o', '|', " |||-/\\|-\\/|---+", "~", "HEAD -> ", " (r)", " (x)");

    /// <summary>
    /// The corners of pane frames and overlays (the graph's own curves are not affected). Settable because the
    /// settings screen changes it while the app runs; the one instance the app draws with is shared, so every view
    /// sees the change.
    /// </summary>
    public FrameStyle Frame { get; set; } = FrameStyle.Rounded;

    /// <summary>This is the ASCII fallback set (whatever its frame style).</summary>
    public bool IsAscii => Lines == Ascii.Lines;

    /// <summary>A horizontal connection.</summary>
    public char Horizontal => Lines[(int)(LineDirections.Left | LineDirections.Right)];

    /// <summary>The character for a cell whose lines go in the given directions.</summary>
    public char For(LineDirections directions) => Lines[(int)directions];

    /// <summary>The character of a frame cell for the given directions; square style swaps the four corners.</summary>
    public char FrameLine(LineDirections directions) => Frame == FrameStyle.Square && !IsAscii
        ? directions switch
        {
            LineDirections.Up | LineDirections.Left => SquareBottomRight,
            LineDirections.Down | LineDirections.Left => SquareTopRight,
            LineDirections.Up | LineDirections.Right => SquareBottomLeft,
            LineDirections.Down | LineDirections.Right => SquareTopLeft,
            _ => For(directions),
        }
        : For(directions);

    /// <summary>The set a terminal with the given Unicode support can show.</summary>
    public static GraphGlyphSet For(bool unicode) => unicode ? Unicode : Ascii;
}
